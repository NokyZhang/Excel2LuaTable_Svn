using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ExcelTools.Scripts.Lua
{
    class lgrammar_table
    {
        //注意：下列范式是根据RO项目的配置文件精简的，并不是完整的lua table EBNF范式
        //luatable 的 EBNF范式
        //tableconstructor ::= '{'[fieldlist]'}'
        //fieldlist ::= field{fieldsep field}[fieldsep]
        //field ::= '['exp']''='exp|Name'='exp|exp
        //fieldsep ::= ','|';'

        //exp ::= Number|String|tableconstructor

        private static StreamReader streamReader;
        private static Dictionary<string, char> keysDic = new Dictionary<string, char>();
        private static Dictionary<string, char> propertyDic = new Dictionary<string, char>();
        private static int currentLine = 2;

        enum ErrorMode
        {
            Complete,
            Simple,
        }
        private static void ThrowError(string key)/*key重复错误*/
        {
            string errorText = "key值 " + key + " 重复出现";
            throw new Exception(errorText);
        }

        private static void ThrowError(ErrorMode mode = ErrorMode.Simple)
        {
            string errorText = String.Empty;
            switch (mode)
            {
                case ErrorMode.Complete:
                    errorText = "line " + currentLine + ": 字符'" + (char)streamReader.Peek() + "'" + "附近有错误";
                    break;
                case ErrorMode.Simple:
                    errorText = "字符'" + (char)streamReader.Peek() + "'" + "附近有错误";
                    break;
            }
            throw new Exception(errorText);
        }

        public static void lgrammar(StreamReader fs)
        {
            streamReader = fs;
            keysDic.Clear();
            propertyDic.Clear();
            GetLuaTable();
        }

        private static void GetLuaTable()
        {
            Ignore_Space_Wrap();
            if (streamReader.Peek() == '{')
            {
                streamReader.Read();
                GetFieldList();
            }
            else
            {
                ThrowError();
            }
            if(!streamReader.EndOfStream && streamReader.Peek() == '}')
            {
                streamReader.Read();
            }
            else
            {
                ThrowError();
            }
        }

        private static void GetFieldList()
        {
            Ignore_Space_Wrap();
            GetField();
            while (streamReader.Peek() != '}' && !streamReader.EndOfStream)
            {
                GetFieldSep();
                GetField();
            }
        }

        private static void GetFieldSep()
        {
            Ignore_Space_Wrap();
            if (streamReader.Peek() == ',' || streamReader.Peek() == ';')
            {
                streamReader.Read();
            }
            else
            {
                ThrowError();
            }
        }

        private static void GetField()
        {
            Ignore_Space_Wrap();
            if (streamReader.EndOfStream)
            {
                return;
            }
            int next = streamReader.Peek();
            if(next == ',' || next == ';')
            {
                ThrowError();
            }
            if(next == '[') /*读key*/
            {
                GetKeyValue();
            }
            else if((next >= 65 && next <=90)   /*大写字母*/
                ||(next >= 97 && next <= 122)   /*小写字母*/
                || next == '_')
            {
                GetProperty();
            }
            else if(next == '}') /*正常遇到table尾*/
            {
                return;
            }
            else /*读取值*/
            {
                GetExp();
            }
        }

        private static void GetKeyValue()
        {
            GetKey();
            Ignore_Space_Wrap();
            if(streamReader.Peek() == '=')
            {
                streamReader.Read();
            }
            else
            {
                ThrowError();
            }
            GetExp();
            void GetKey()
            {
                streamReader.Read();
                GetExp();
                string key = buff2string();
                if (!keysDic.ContainsKey(key))
                    keysDic.Add(buff2string(), ' ');
                else
                    ThrowError(key);
                if (streamReader.Peek() == ']')
                {
                    streamReader.Read();
                }
                else
                {
                    ThrowError();
                }
            }
        }

        private static void GetProperty()
        {
            GetName();
            Ignore_Space_Wrap();
            if (streamReader.Peek() == '=')
            {
                streamReader.Read();
            }
            else
            {
                ThrowError();
            }
            GetExp();
            void GetName()
            {
                while(!streamReader.EndOfStream && streamReader.Peek() != '=')
                {
                    if ((streamReader.Peek() >= 65 && streamReader.Peek() <= 90)   /*大写字母*/
                    || (streamReader.Peek() >= 97 && streamReader.Peek() <= 122)   /*小写字母*/
                    || streamReader.Peek() == '_')
                    {
                        streamReader.Read();
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }

        private static List<char> buff = new List<char>();
        private static string buff2string()
        {
            return new string(buff.ToArray());
        }
        private static void GetExp()
        {
            buff.Clear();
            Ignore_Space_Wrap();
            int next = streamReader.Peek();
            if ((next >= 48 && next <= 57)   /*数字*/
               || next == '.')
            {
                GetNumber();
            }
            else if (next == '\'' || next == '"')
            {
                GetString(next);               
            }
            else if (next == '{')
            {
                GetLuaTable();
            }
            void GetNumber()
            {
                    while (!streamReader.EndOfStream)
                    {
                        if ((streamReader.Peek() >= 48 && streamReader.Peek() <= 57)   /*数字*/
                           || streamReader.Peek() == '.')
                        {
                            buff.Add((char)streamReader.Read());
                        }
                        else
                        {
                            return;
                        }
                    }
                }
            void GetString(int quotationMark)
            {
                streamReader.Read();
                while (!streamReader.EndOfStream && streamReader.Peek() != quotationMark)
                {
                    buff.Add((char)streamReader.Read());
                }
                if(streamReader.Peek() != quotationMark)
                {
                    ThrowError();
                }
                else
                {
                    streamReader.Read();
                }
            }
        }

        private static void Ignore_Space_Wrap()
        {
            while (!streamReader.EndOfStream)
            {
                switch (streamReader.Peek())
                {
                    case '\n':case '\r':/* 跳过换行符 */
                        AddLineNumber();
                        break;
                    case ' ':case '\f':case '\t':case '\v':/* 跳过空格 */
                        streamReader.Read();
                        break;
                    default:/* 遇到内容函数返回*/
                        return;
                }
            }
        }

        private static void AddLineNumber()
        {
            currentLine++;
            streamReader.Read();
        }
    }
}
