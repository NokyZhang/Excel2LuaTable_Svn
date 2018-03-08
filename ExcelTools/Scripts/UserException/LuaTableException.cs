using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExcelTools.Scripts.UserException
{
    class LuaTableException:Exception
    {
        public LuaTableException(string message):base(message)
        {
        }
    }
}
