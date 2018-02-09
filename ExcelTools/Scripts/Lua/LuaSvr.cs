using System;
using System.Collections;

namespace Lua
{
    public enum LuaSvrFlag
    {
        LSF_BASIC = 0,
        LSF_DEBUG = 1,
        LSF_EXTLIB = 2,
        LSF_3RDDLL = 4
    }

    public class LuaSvr
    {
        public LuaState luaState;
        public bool inited = false;

        int errorReported;

        public LuaSvr() { }

        private volatile int bindProgress = 0;
        private void doBind(object state)
        {
            IntPtr L = (IntPtr)state;
            List<Action<IntPtr>> list = new List<Action<IntPtr>>();
        }
    }
}