using ExcelTools.Scripts;
using ExcelTools.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lua.llex_lite;

namespace Lua
{
    public class lparser
    {
        static string configformat = "[{0}] = {{";
        static string kvformat = "{0} = {1}";
        public class table
        {
            public string sourceExlpath;
            public string md5;
            public string name;
            public List<config> configs = new List<config>();
            public Dictionary<string, config> configsDic = new Dictionary<string, config>();

            //记录原来配置 用于回退
            private config oldCfg = null;
            private int oldIndex = -1;

            public table(string _md5, string _name, string _sourceExlPath)
            {
                md5 = _md5;
                name = _name;
                sourceExlpath = _sourceExlPath;
            }

            public table(table t)
            {
                md5 = t.md5;
                name = t.name;
            }

            /// <summary>
            /// </summary>
            /// <param name="out4Server"> 为true时，返回服务器端所需的配置（是完整的配置）；为false时，会根据Excel中的第一行的设置返回</param>
            public string GenString(Func<string> callback = null, bool out4Server = true)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("--" + md5 + "\n");
                sb.Append(name + " = {\n");
                int rows = 0;
                for (int i = 0; i < configs.Count; i++)
                {
                    AppendConfig(ref sb, configs[i], out4Server);
                    rows++;
                }
                //if (filter != null && filter.deletedrows.Count > 0)//是否需要追加若干行配置
                //{
                //    AppendExcelDelRows(filter.deletedrows, ref sb);
                //    rows++;
                //}
                if (rows > 0)
                    sb.Remove(sb.Length - 2, 1);//删除最后一行的逗号 ','
                sb.Append("}\n");
                if(callback != null)
                    sb.Append(callback());
                sb.Append("return " + name);
                return sb.ToString();
            }

            void AppendExcelDelRows(Dictionary<string, config> confDic, ref StringBuilder sb)
            {
                foreach(var conf in confDic.Values)
                    AppendConfig(ref sb, conf);
            }

            void AppendConfig(ref StringBuilder sb, config conf, bool out4Server = true)
            {
                sb.Append("\t");
                sb.Append(conf.GenString(out4Server));
                sb.Append(",");
                sb.Append("\n");
            }

            #region 应用修改
            public void Apply(string status, config cfg, string key = null)
            {
                switch (status)
                {
                    case DifferController.STATUS_ADDED:
                        AddConfig(cfg);
                        break;
                    case DifferController.STATUS_DELETED:
                        RemoveConfig(key);
                        break;
                    case DifferController.STATUS_MODIFIED:
                        ModifyConfig(cfg);
                        break;
                    default: break;
                }
            }

            void RemoveConfig(string key)
            {
                if (configsDic.ContainsKey(key))
                {
                    oldCfg = configsDic[key];
                    oldIndex = configs.IndexOf(configsDic[key]);
                    config realCfg = config.GenConfigByIsNeedGenDic(configsDic[key], null);
                    if (realCfg == null)
                    {
                        configs.Remove(configsDic[key]);
                        configsDic.Remove(key);
                    }
                    else
                    {
                        int index = configs.IndexOf(configsDic[key]);
                        configs[index] = realCfg;
                        configsDic[key] = realCfg;
                    }
                }
            }

            void AddConfig(config cfg)
            {
                if (!configsDic.ContainsKey(cfg.key))
                {
                    config realCfg = config.GenConfigByIsNeedGenDic(null, cfg);
                    configsDic[cfg.key] = realCfg;
                    configs.Add(realCfg);
                }
            }

            void ModifyConfig(config cfg)
            {
                if (configsDic.ContainsKey(cfg.key))
                {
                    oldCfg = configsDic[cfg.key];
                    int index = configs.IndexOf(configsDic[cfg.key]);
                    config realCfg = config.GenConfigByIsNeedGenDic(configsDic[cfg.key], cfg);
                    configs[index] = realCfg;
                    configsDic[cfg.key] = realCfg;
                }
            }
            #endregion

            #region 回退修改
            public void Cancel(string key)
            {
                //回退修改和删除
                if(oldCfg != null)
                {
                    int index;
                    if (!configsDic.ContainsKey(key))
                    {
                        index = oldIndex;
                    }
                    else
                    {
                        index = configs.IndexOf(configsDic[key]);
                    }
                    configs[index] = oldCfg;
                    configsDic[key] = oldCfg;
                }
                else
                {
                    configs.Remove(configsDic[key]);
                    configsDic.Remove(key);
                }
                oldCfg = null;
                oldIndex = -1;
            }
            #endregion
        }

        public class config
        {
            public string key;
            public List<property> properties = new List<property>();
            public Dictionary<string, property> propertiesDic = new Dictionary<string, property>();
            private Dictionary<string, bool> _IsNeedGenDic = new Dictionary<string, bool>();

            public Dictionary<string, bool> IsNeedGenDic
            {
                get
                {
                    return _IsNeedGenDic;
                }
            }

            public config(string k)
            {
                key = k;
            }

            public config(config config)
            {
                key = config.key;
                property property;
                for(int i = 0; i < config.properties.Count; i++)
                {
                    property = new property(config.properties[i]);
                    properties.Add(property);
                    propertiesDic.Add(property.name, property);
                    _IsNeedGenDic.Add(property.name, config.IsNeedGenDic[property.name]);
                }
            }

            public string GenString(bool out4Server = true)
            {
                StringBuilder sb = new StringBuilder(string.Format(configformat, key));
                int cells = 0;
                for(int i = 0; i < properties.Count; i++)
                {
                    if (out4Server
                        || (!out4Server && !properties[i].isServer))
                    {
                        AppendProperty(ref sb, properties[i], out4Server);
                        cells++;
                    }
                }
                if (cells > 0)//删除最后一行的逗号和空格 ", "
                    sb.Remove(sb.Length - 2, 2);
                sb.Append("}");
                return sb.ToString();
            }

            void AppendProperty(ref StringBuilder sb, property p, bool out4Server = true)
            {
                sb.Append(p.GenString());
                sb.Append(", ");
            }

            public void Apply(string status, property p)
            {
                switch (status)
                {
                    case DifferController.STATUS_ADDED:
                        AddProperty(p);
                        break;
                    case DifferController.STATUS_DELETED:
                        RemoveProperty(p);
                        break;
                    case DifferController.STATUS_MODIFIED:
                        ModifyProperty(p);
                        break;
                    default: break;
                }
            }

            void RemoveProperty(property p)
            {
                if (propertiesDic.ContainsKey(p.name))
                {
                    properties.Remove(propertiesDic[p.name]);
                    propertiesDic.Remove(p.name);
                    IsNeedGenDic.Remove(p.name);
                }
            }

            void AddProperty(property p)
            {
                if (!propertiesDic.ContainsKey(p.name))
                {
                    propertiesDic[p.name] = p;
                    properties.Add(p);
                    IsNeedGenDic.Add(p.name, true);
                }
            }

            void ModifyProperty(property p)
            {
                if (propertiesDic.ContainsKey(p.name))
                {
                    int index = properties.IndexOf(propertiesDic[p.name]);
                    properties[index] = p;
                    propertiesDic[p.name] = p;
                }
            }

            public void ResetIsNeedGen(string propertyEname, bool isNeedGen)
            {
                if (_IsNeedGenDic.ContainsKey(propertyEname))
                {
                    _IsNeedGenDic[propertyEname] = isNeedGen;
                }
            }

            public static config GenConfigByIsNeedGenDic(config baseCfg, config newCfg)
            {
                config realCfg = new config(newCfg);
                config originCfg = new config(baseCfg);
                if (realCfg == null)
                {
                    if (originCfg != null) {
                        for (int i = 0; i < originCfg.properties.Count; i++)
                        {
                            if (originCfg.IsNeedGenDic[originCfg.properties[i].name])
                            {
                                originCfg.RemoveProperty(originCfg.properties[i]);
                            }
                        }
                        realCfg = originCfg;
                    }
                }
                else
                {
                    for (int i = 0; i < realCfg.properties.Count; i++)
                    {
                        if (!realCfg.IsNeedGenDic[realCfg.properties[i].name])
                        {
                            if (originCfg != null && originCfg.propertiesDic.ContainsKey(realCfg.properties[i].name))
                            {
                                realCfg.ModifyProperty(originCfg.propertiesDic[realCfg.properties[i].name]);
                            }
                            else
                            {
                                realCfg.RemoveProperty(realCfg.properties[i]);
                            }
                        }
                    }
                }
                return realCfg;
            }
        }

        public class property
        {
            public string name;
            public string value;
            public bool isServer = false;

            public property() { }

            public property(property property)
            {
                name = property.name;
                value = property.value;
                isServer = property.isServer;
            }

            public string GenString()
            {
                return string.Format(kvformat, name, value);
            }
        }

        static string read_name(StreamReader sr)
        {
            string ret = null;
            StringBuilder sb = new StringBuilder();
            while (!sr.EndOfStream && sr.Peek() != '=')
            {
                sb.Append((char)sr.Read());
            }
            return ret;
        }

        static property read_property(StreamReader sr, string sourceExlPath)
        {
            property p = new property();
            Excel sourceExl = GlobalCfg.Instance.GetParsedExcel(sourceExlPath, false);
            p.name = llex_lite.buff2str();/* read key */
            if (sourceExl.PropertyDic.ContainsKey(p.name)) {
                p.isServer = sourceExl.PropertyDic[p.name].isServerProperty;
            }
            llex_lite.llex(sr); p.value = llex_lite.buff2str();/* read val */
            return p;
        }

        static config read_config(StreamReader sr, string sourceExlPath)
        {
            //llex_lite.llex(sr); /* read key */
            config config = new config(llex_lite.buff2str());/* read key */
            llex_lite.llex(sr, true);/* skip '{' */
            string k, v = null;
            while (!sr.EndOfStream && llex_lite.llex(sr) != '}')
            {
                property p = read_property(sr, sourceExlPath);
                config.properties.Add(p);
                config.propertiesDic.Add(p.name, p);
                config.IsNeedGenDic.Add(p.name, true);
            }
            return config;
        }

        static table read_table(StreamReader sr, string sourceExlPath)
        {
            string md5Str = read_md5comment(sr);
            llex_lite.llex(sr); /* read key */
            table t = new table(md5Str, llex_lite.buff2str(), sourceExlPath);
            llex_lite.llex(sr, true);/* skip '{' */
            while(!sr.EndOfStream && llex_lite.llex(sr) != '}')
            {
                config conf = read_config(sr, sourceExlPath);
                t.configs.Add(conf);
                t.configsDic.Add(conf.key, conf);
            }
            return t;
        }

        static string read_md5comment(StreamReader sr)
        {
            int e = llex_lite.llex(sr);
            Debug.Assert(e == (int)LEXTYPE.COMMENT);
            return llex_lite.buff2str();
        }

        static void read_file(string path, string sourceExlPath)
        {
            StreamReader sr = new StreamReader(path);
            table t = read_table(sr, sourceExlPath);
            StringBuilder sb = new StringBuilder();
            Console.WriteLine("md5 = " + t.md5 + " tablename = " + t.name);
            for(int i = 0; i < t.configs.Count; i++)
            {
                config conf = t.configs[i];
                sb.Append(conf.key + " = ");
                for(int j = 0; j < conf.properties.Count; j++)
                {
                    property p = conf.properties[j];
                    sb.Append(p.name + " = " + p.value + " ");
                }
                Console.WriteLine(sb.ToString());
                sb.Clear();
            }
        }

        public static table parse(string path, string sourceExlpath)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                return read_table(sr, sourceExlpath);
            }
        }

        public static string ReadTableMD5(string path)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                string md5comment = read_md5comment(sr);
                return md5comment.Substring(4, md5comment.Length - 4);
            }
        }
    }
}
