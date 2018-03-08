using ExcelTools.Scripts.UserException;
using System.Collections.Generic;
using System.IO;

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
        private static Stack<Dictionary<string, char>> keysColsureStack = new Stack<Dictionary<string, char>>();

        private static void Add2keysDic(string key)
        {
            Dictionary<string, char> keysDic = keysColsureStack.Peek();
            if (!keysDic.ContainsKey(key))
                keysDic.Add(key, ' ');
            else
                ThrowError(key);
        }

        private static void ThrowError(string key)/*key重复错误*/
        {
            string errorText = "key值 " + key + " 重复出现";
            throw new LuaTableException(errorText);
        }

        private static void ThrowError()
        {
            string errorText = "字符'" + (char)streamReader.Peek() + "'" + "附近有错误";
            throw new LuaTableException(errorText);
        }

        /// <summary> 
        /// 仅语法检查的重载方法
        /// </summary> 
        public static void lgrammar(StreamReader sr)
        {
            IsNeedRecord = false;
            streamReader = sr;
            keysColsureStack.Clear();
            GetLuaTable();
        }

        private static List<char> mainBuffer = new List<char>();
        /// <summary> 
        /// 需要存储内容的重载方法
        /// </summary> 
        public static void lgrammar(StreamReader sr, List<char> buffer)
        {
            IsNeedRecord = true;
            streamReader = sr;
            keysColsureStack.Clear();
            mainBuffer = buffer;
            GetLuaTable();
        }

        private static void GetLuaTable()
        {
            Ignore_Space_Wrap();
            if (streamReader.Peek() == '{')
            {
                keysColsureStack.Push(new Dictionary<string, char>()); //添加新table的key表
                ReadStream();
                GetFieldList();
            }
            else
            {
                ThrowError();
            }
            Ignore_Space_Wrap();
            if (!streamReader.EndOfStream && streamReader.Peek() == '}')
            {
                keysColsureStack.Pop();
                ReadStream();
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
                ReadStream();
            }
            else if(streamReader.Peek() == '}')
            {
                return;
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
            else if( NextIsLetter()
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
                ReadStream();
            }
            else
            {
                ThrowError();
            }
            if (NextIsLetter()
                || streamReader.Peek() == '_')
            {
                GetName();
            }
            else
            {
                GetExp();
            }
            void GetKey()
            {
                ReadStream();
                GetExp();
                Add2keysDic(GetCurExp());
                if (streamReader.Peek() == ']')
                {
                    ReadStream();
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
            Add2keysDic(GetCurExp());
            Ignore_Space_Wrap();
            if (streamReader.Peek() == '=')
            {
                streamReader.Read();
            }
            else
            {
                return;
            }
            if (NextIsLetter()
                || streamReader.Peek() == '_')
            {
                GetName();
            }
            else
            {
                GetExp();
            }
        }

        private static void GetName()
        {
            expBuffer.Clear();
            ReadStream(); //读取变量的首字母
            expBuffer.Add(mainBuffer[mainBuffer.Count - 1]);
            while (!streamReader.EndOfStream && streamReader.Peek() != '=')
            {
                if (NextIsLetter()
                || streamReader.Peek() == '_'
                || NextIsNumber())
                {
                    ReadStream();
                    expBuffer.Add(mainBuffer[mainBuffer.Count - 1]);
                }
                else
                {
                    return;
                }
            }
        }

        private static List<char> expBuffer = new List<char>();
        private static string GetCurExp()
        {
            string result = new string(expBuffer.ToArray());
            expBuffer.Clear();
            return result;
        }
        private static void GetExp()
        {
            expBuffer.Clear();
            Ignore_Space_Wrap();
            int next = streamReader.Peek();
            if ( NextIsNumber()
               || next == '.'
               || next == '-')
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
            else if (next == 'f' || next == 't')
            {
                GetBool();
            }
            void GetNumber()
            {
                bool hasDot = false;
                bool isHex = false;
                char first = ReadStream();
                expBuffer.Add(mainBuffer[mainBuffer.Count - 1]);
                if (first == '.')
                {
                    hasDot = true;
                }
                else if(first == '0')
                {
                    if(streamReader.Peek() == 'x' || streamReader.Peek() == 'X') /*十六进制*/
                    {
                        isHex = true;
                        ReadStream();
                        expBuffer.Add(mainBuffer[mainBuffer.Count - 1]);
                    }
                }
                
                while (!streamReader.EndOfStream)
                {
                    if ( NextIsNumber()
                      || (streamReader.Peek() == '.'&& !hasDot))
                    {
                        if (ReadStream() == '.')
                        {
                            hasDot = true;
                        }
                        expBuffer.Add(mainBuffer[mainBuffer.Count-1]);
                    }
                    else if(isHex && NextIsHex())
                    {
                        ReadStream();
                        expBuffer.Add(mainBuffer[mainBuffer.Count - 1]);
                    }
                    else
                    {
                        return;
                    }
                }
            }

            void GetString(int quotationMark)
            {
                ReadStream(); /*读取前引号*/
                while (!streamReader.EndOfStream && streamReader.Peek() != quotationMark)
                {
                    ReadStream();
                    expBuffer.Add(mainBuffer[mainBuffer.Count - 1]);
                }
                if(streamReader.Peek() != quotationMark)
                {
                    ThrowError();
                }
                else
                {
                    ReadStream(); /*读取后引号*/
                }
            }

            void GetBool()
            {
                char first = ReadStream();
                List<char> temp = new List<char>();
                temp.Add(first);
                if (first == 'f')
                {
                    for(int i = 0; i < 4; i++)
                    {
                        temp.Add(ReadStream());
                    }
                    string value = new string(temp.ToArray());
                    if(value != "false")
                    {
                        ThrowError();
                    }
                }
                else if(first == 't')
                {
                    for (int i = 0; i < 3; i++)
                    {
                        temp.Add(ReadStream());
                    }
                    string value = new string(temp.ToArray());
                    if (value != "true")
                    {
                        ThrowError();
                    }
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
                        ReadStream();
                        break;
                    case ' ':case '\f':case '\t':case '\v':/* 跳过空格 */
                        ReadStream();
                        break;
                    default:/* 遇到内容函数返回*/
                        return;
                }
            }
        }

        private static bool IsNeedRecord = false;
        private static char ReadStream()
        {
            char c = (char)streamReader.Read();
            if (IsNeedRecord)
            {
                mainBuffer.Add(c);
            }
            return c;
        }

        private static void SkipStream()
        {
            streamReader.Read();
        }

        private static bool NextIsLetter()
        {
            if((streamReader.Peek() >= 65 && streamReader.Peek() <= 90)   /*大写字母*/
               || (streamReader.Peek() >= 97 && streamReader.Peek() <= 122))   /*小写字母*/
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool NextIsNumber()
        {
            if ((streamReader.Peek() >= 48 && streamReader.Peek() <= 57))  /*数字*/
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        private static bool NextIsHex()
        {
            if ((streamReader.Peek() >= 48 && streamReader.Peek() <= 57)
                || (streamReader.Peek() >= 65 && streamReader.Peek() <= 70) 
                ||(streamReader.Peek() >= 97 && streamReader.Peek() <= 102))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
