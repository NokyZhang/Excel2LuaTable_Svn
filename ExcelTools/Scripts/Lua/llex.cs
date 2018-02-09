using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

namespace Lua
{
    class llex
    {
        const int FIRST_RESERVED = 257;
        const int UCHAR_MAX = 0xff;

        /* ORDER RESERVED */
        static List<string> luaX_tokens = new List<string>(){
            "and", "break", "do", "else", "elseif",
            "end", "false", "for", "function", "goto", "if",
            "in", "local", "nil", "not", "or", "repeat",
            "return", "then", "true", "until", "while",
            "//", "..", "...", "==", ">=", "<=", "~=",
            "<<", ">>", "::", "<eof>",
            "<number>", "<integer>", "<name>", "<string>"
        };

        /*
        * WARNING: if you change the order of this enumeration,
        * grep "ORDER RESERVED"
        */
        enum RESERVED
        {
            /* terminal symbols denoted by reserved words */
            TK_AND = FIRST_RESERVED, TK_BREAK,
            TK_DO, TK_ELSE, TK_ELSEIF, TK_END, TK_FALSE, TK_FOR, TK_FUNCTION,
            TK_GOTO, TK_IF, TK_IN, TK_LOCAL, TK_NIL, TK_NOT, TK_OR, TK_REPEAT,
            TK_RETURN, TK_THEN, TK_TRUE, TK_UNTIL, TK_WHILE,
            /* other terminal symbols */
            TK_IDIV, TK_CONCAT, TK_DOTS, TK_EQ, TK_GE, TK_LE, TK_NE,
            TK_SHL, TK_SHR,
            TK_DBCOLON, TK_EOS,
            TK_FLT, TK_INT, TK_NAME, TK_STRING
        };

        /* number of reserved words */
        const int NUM_RESERVED = (int)RESERVED.TK_WHILE - FIRST_RESERVED + 1;

        static bool currIsNewline(StreamReader sr)
        {
            return sr.Peek() == '\n' || sr.Peek() == '\r';
        }

        static void inclinenumber(StreamReader sr)
        {
            int old = sr.Peek();
            Debug.Assert(currIsNewline(sr));
            sr.Read();
            if (currIsNewline(sr) && sr.Peek() != old)
                sr.Read();
        }

        static List<char> buffer = new List<char>();
        static void resetbuffer()
        {
            buffer.Clear();
        }

        static bool check_next1(StreamReader sr, char c)
        {
            if ((char)sr.Peek() == c)
            {
                next(sr);
                return true;
            }
            else return false;
        }

        static bool check_next2(StreamReader sr, string s)
        {
            Debug.Assert(s[2] == '\0');
            char c = (char)sr.Peek();
            if (c == s[0] || c == s[1])
            {
                save_and_next(sr);
                return true;
            }
            return false;
        }

        static bool lislalpha(int n)
        {
            char c = (char)n;
            return char.IsLetter(c) || c == '_';
        }

        static bool lislalpha(char c)
        {
            return char.IsLetter(c) || c == '_';
        }

        static bool lislalnum(int n)
        {
            char c = (char)n;
            return char.IsLetterOrDigit(c) || c == '_';
        }

        static bool lislalnum(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        static bool lisdigit(int n)
        {
            char c = (char)n;
            return char.IsDigit(c);
        }

        static bool lisdigit(char c)
        {
            return char.IsDigit(c);
        }

        static bool lisxdigit(int n)
        {
            char c = (char)n;
            return char.IsDigit(c) || ('a' <= c && c <= 'f') || ('A' <= c && c <= 'F');
        }

        static bool lisxdigit(char c)
        {
            return char.IsDigit(c) || ('a' <= c && c <= 'f') || ('A' <= c && c <= 'F');
        }

        static bool lisspace(int n)
        {
            char c = (char)n;
            return char.IsWhiteSpace(c);
        }

        static bool lisspace(char c)
        {
            return char.IsWhiteSpace(c);
        }

        static int isreserved(string ts)
        {
            return luaX_tokens.IndexOf(ts);
        }

        static int next(StreamReader sr)
        {
            return sr.Read();
        }

        static char nextChar(StreamReader sr)
        {
            return (char)sr.Read();
        }

        static void save(int n)
        {
            char c = (char)n;
            buffer.Add(c);
        }

        static void save(char c)
        {
            buffer.Add(c);
        }

        static void save(StreamReader sr)
        {
            buffer.Add((char)sr.Peek());
        }

        static void remove(int n)
        {
            buffer.RemoveRange(buffer.Count - n, n);
        }

        static void save_and_next(StreamReader sr)
        {
            buffer.Add((char)sr.Read());
        }

        static void esccheck(StreamReader sr, bool b, string msg)
        {
            if (!b)
            {
                if (!sr.EndOfStream)
                    save_and_next(sr); /* add current to buffer for error message */
                Console.Error.WriteLine(string.Format("{0} near <string>", msg));
            }
        }

        static int lua0_hexavalue(int c)
        {
            if (lisdigit(c)) return c - '0';
            else return (char.ToLower((char)c) - 'a') + 10;
        }

        static int gethexa(StreamReader sr)
        {
            save_and_next(sr);
            esccheck(sr, lisxdigit(sr.Peek()), "hexadecimal digit expected");
            return lua0_hexavalue(sr.Peek());
        }

        static int read_hexaesc(StreamReader sr)
        {
            int r = gethexa(sr);
            r = (r << 4) + gethexa(sr);
            remove(2);
            return r;
        }

        /// <summary>
        /// utf8转义，待实现
        /// </summary>
        /// <param name="sr"></param>
        static void utf8esc(StreamReader sr)
        {

        }

        static int read_decesc(StreamReader sr)
        {
            int i;
            int r = 0; /* result accumulator */
            for (i = 0; i < 3 && lisdigit(sr.Peek()); i++)
            {
                r = 10 * r + sr.Peek() - '0';
                save_and_next(sr);
            }
            esccheck(sr, r <= UCHAR_MAX, "decimal escape too large");
            remove(i);
            return r;
        }

        /* LUA_NUMBER */
        /*
         * this function is quite liberal in what it accepts, as 'lua0_str2num'
         * will reject ill-formed numerals.
         */
        static int read_numeral(StreamReader sr)
        {
            string expo = "Ee";
            int first = sr.Peek();
            Debug.Assert(lisdigit(first));
            save_and_next(sr);
            if (first == '0' && check_next2(sr, "xX")) /* hexadecimal? */
                expo = "Pp";
            for (; ; )
            {
                if (check_next2(sr, expo))/* exponent part? */
                    check_next2(sr, "-+");/* optional exponent sign */
                if (lisxdigit(sr.Peek()))
                    save_and_next(sr);
                else if (sr.Peek() == '.')
                    save_and_next(sr);
                else break;
            }
            save('\0');
            string numstr = new string(buffer.ToArray());
            int nret;
            float fret;
            if (int.TryParse(numstr, out nret))
                return (int)RESERVED.TK_INT;
            else if (float.TryParse(numstr, out fret))
                return (int)RESERVED.TK_FLT;
            else
                Console.Error.WriteLine("malformed number");
            return (int)RESERVED.TK_FLT;
        }

        /*
        ** skip a sequence '[=*[' or ']=*]'; if sequence is well formed, return
        ** its number of '='s; otherwise, return a negative number (-1 iff there
        ** are no '='s after initial bracket)
        */
        static int skip_sep(StreamReader sr)
        {
            int count = 0;
            int s = sr.Peek();
            Debug.Assert(s == '[' || s == ']');
            save_and_next(sr);
            while ((char)sr.Peek() == '=')
            {
                save_and_next(sr);
                count++;
            }
            return sr.Peek() == s ? count : (-count) - 1;
        }

        static void read_long_string(StreamReader sr, int sep, string seminfo = null)
        {
            save_and_next(sr);/* skip 2nd '[' */
            if (currIsNewline(sr))/* string starts with a newline? */
                inclinenumber(sr);/* skip it */
            while (!sr.EndOfStream)
            {
                switch ((char)sr.Peek())
                {
                    case ']':
                        if (skip_sep(sr) == sep)
                        {
                            save_and_next(sr);
                            goto endloop;
                        }
                        break;
                    case '\n':
                    case '\r':
                        save('\n');
                        inclinenumber(sr);
                        if (seminfo == null) resetbuffer();
                        break;
                    default:
                        if (seminfo != null) save_and_next(sr);
                        else sr.Read();
                        break;
                }
            }
            if (sr.EndOfStream)
            {
                string what = seminfo == null ? "comment" : "string";
                Console.Error.WriteLine(string.Format("unfinished long {0}", what));
            }
            endloop:
            if (seminfo != null)
                seminfo = new string(buffer.ToArray(), sep + 2, buffer.Count - 2 * (sep + 2));
        }

        static void read_string(StreamReader sr, int del, string seminfo)
        {
            save_and_next(sr);/* keep delimiter (for error messages) */
            while (sr.Peek() != del)
            {
                switch (sr.Peek())
                {
                    case '\0':
                        Console.Error.WriteLine(@"unfinished string. last char is \0");
                        break;/* to avoid warnings */
                    case '\n':
                    case '\r':
                        Console.Error.WriteLine(@"unfinished string. last char is \n or \r");
                        break;/* to avoid warnings */
                    case '\\':/* escape sequences */
                        {
                            int c = 0;/* final character to be saved */
                            save_and_next(sr);/* keep '\\' for error messages */
                            switch (sr.Peek())
                            {
                                case 'a': c = '\a'; goto read_save;
                                case 'b': c = '\b'; goto read_save;
                                case 'f': c = '\f'; goto read_save;
                                case 'n': c = '\n'; goto read_save;
                                case 'r': c = '\r'; goto read_save;
                                case 't': c = '\t'; goto read_save;
                                case 'v': c = '\v'; goto read_save;
                                case 'x': c = read_hexaesc(sr); goto read_save;
                                case 'u': utf8esc(sr); goto no_save;
                                case '\n':
                                case '\r':
                                    inclinenumber(sr); c = '\n'; goto only_save;
                                case '\\':
                                case '\"':
                                case '\'':
                                    c = sr.Peek(); goto read_save;
                                case '\0': goto no_save;/* will raise an error next loop */
                                case 'z':/* zap following span of sapces */
                                    {
                                        remove(1);/* remove '\\' */
                                        next(sr);/* skip the 'z' */
                                        while (lisspace(sr.Peek()))
                                        {
                                            if (currIsNewline(sr)) inclinenumber(sr);
                                            else next(sr);
                                        }
                                        goto no_save;
                                    }
                                default:
                                    {
                                        esccheck(sr, lisdigit(sr.Peek()), "invalid escape sequence");
                                        c = read_decesc(sr);/* digital escape '\ddd' */
                                        goto only_save;
                                    }
                            }
                            read_save:
                            next(sr);
                            /* go through */
                            only_save:
                            remove(1);/* remove '\\' */
                            save(c);
                            /* go through */
                            no_save: break;
                        }
                    default:
                        save_and_next(sr);
                        break;
                }
            }
            save_and_next(sr); /* skip delimiter */
            seminfo = new string(buffer.ToArray(), 1, buffer.Count - 2);
        }

        static int lex(StreamReader sr, string seminfo)
        {
            while (!sr.EndOfStream)
            {
                switch (sr.Peek())
                {
                    case '\n':
                    case '\r':/* line breaks */
                        inclinenumber(sr);
                        break;
                    case ' ':
                    case '\f':
                    case '\t':
                    case '\v':/* spaces */
                        sr.Read();
                        break;
                    case '-':/* '-' or '--' (comment) */
                        {
                            sr.Read();
                            if ((char)sr.Peek() != '-') return '-';
                            /* else is a comment */
                            sr.Read();
                            if ((char)sr.Peek() == '[')/* long comment? */
                            {
                                int sep = skip_sep(sr);
                                resetbuffer(); /* 'skip_sep' may dirty the buffer */
                                if (sep >= 0)
                                {
                                    read_long_string(sr, sep);/* skip long comment */
                                    resetbuffer();/* previous call may dirty the buffer */
                                    break;
                                }
                            }
                            /* else short commetn */
                            while (!currIsNewline(sr) && !sr.EndOfStream)
                                sr.Read();/* skip until end of line (or end of file) */
                        }
                        break;
                    case '[':/* long string or simply '[' */
                        {
                            int sep = skip_sep(sr);
                            if (sep >= 0)
                            {
                                read_long_string(sr, sep, seminfo);
                                return (int)RESERVED.TK_STRING;
                            }
                            else if (sep != -1)/* '[=..' missing second bracket */
                                Console.Error.WriteLine("invalid long string delimiter");
                            return '[';
                        }
                    case '=':
                        sr.Read();
                        if (check_next1(sr, '=')) return (int)RESERVED.TK_EQ;
                        else return '=';
                    case '<':
                        next(sr);
                        if (check_next1(sr, '=')) return (int)RESERVED.TK_LE;
                        else if (check_next1(sr, '<')) return (int)RESERVED.TK_SHL;
                        else return '<';
                    case '>':
                        next(sr);
                        if (check_next1(sr, '=')) return (int)RESERVED.TK_GE;
                        else if (check_next1(sr, '>')) return (int)RESERVED.TK_SHR;
                        else return '>';
                    case '/':
                        next(sr);
                        if (check_next1(sr, '/')) return (int)RESERVED.TK_IDIV;
                        else return '/';
                    case '~':
                        next(sr);
                        if (check_next1(sr, '=')) return (int)RESERVED.TK_NE;
                        else return '~';
                    case ':':
                        next(sr);
                        if (check_next1(sr, ':')) return (int)RESERVED.TK_DBCOLON;
                        else return ':';
                    case '"':
                    case '\'':/* short literal string */
                        read_string(sr, sr.Peek(), seminfo);
                        return (int)RESERVED.TK_STRING;
                    case '.': /* '.', '..', '...' or number */
                        save_and_next(sr);
                        if (check_next1(sr, '.'))
                            if (check_next1(sr, '.'))
                                return (int)RESERVED.TK_DOTS;/* '...' */
                            else return (int)RESERVED.TK_CONCAT;/* '..' */
                        else if (!lisdigit(sr.Peek())) return '.';
                        else return read_numeral(sr);
                    case '0': case '1': case '2': case '3': case '4':
                    case '5': case '6': case '7': case '8': case '9':
                        return read_numeral(sr);
                    case -1:
                        return (int)RESERVED.TK_EOS;
                    default:
                        if (lislalpha(sr.Peek()))/* identifier or reserved word? */
                        {
                            do { save_and_next(sr); } while (lislalnum(sr.Peek()));
                            seminfo = new string(buffer.ToArray());
                            int n = isreserved(seminfo);
                            if (n > -1)
                                return n + FIRST_RESERVED;
                            else
                                return (int)RESERVED.TK_NAME;
                        }
                        return next(sr);
                }
            }
            return (int)RESERVED.TK_EOS;
        }

    }
}
