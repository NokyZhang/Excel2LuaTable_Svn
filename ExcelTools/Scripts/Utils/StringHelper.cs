using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelTools.Scripts.Utils
{
    static class StringHelper
    {
        /// <summary> 
        /// 字符串匹配 
        /// </summary> 
        /// <param name="ss">源字符串</param> 
        /// <param name="ps">模式串</param> 
        /// <param name="MatchFromStart">是否强制从头匹配</param>
        /// <param name="IsOrdinalIgnore">是否大小写敏感</param>
        /// <returns></returns>
        public static bool StringMatch(string ss, string ps, bool MatchFromStart, bool IsOrdinalIgnore = true)
        {
            if(ps == null)
            {
                return true;
            }
            if (!IsOrdinalIgnore)
            {
                ss = ss.ToLower();
                ps = ps.ToLower();
            }
            char[] s = ss.ToCharArray();
            char[] p = ps.ToCharArray();

            int i = 0; //主串的位置
            int j = 0; //模式串的位置
            while(i < s.Length && j < p.Length)
            {
                if (j == 0 && i > s.Length - p.Length)
                    break;
                if(s[i] == p[j])
                {
                    if (i==0 && !MatchFromStart)
                    {
                        break;
                    }
                    i++;
                    j++;
                }
                else
                {
                    if (MatchFromStart)
                    {
                        break;
                    }
                    i = i - j + 1;
                    j = 0;
                }
            }
            if (j == p.Length && p.Length != 0)
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
