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
            public string md5;
            public string name;
            public List<config> configs = new List<config>();
            public Dictionary<string, config> configsDic = new Dictionary<string, config>();

            //记录原来配置 用于回退
            private config oldCfg = null;

            public table(string _md5, string _name)
            {
                md5 = _md5;
                name = _name;
            }

            public table(table t)
            {
                md5 = t.md5;
                name = t.name;
            }

            public string GenString(Func<string> callback = null, tablediff filter = null)
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("--" + md5 + "\n");
                sb.Append(name + " = {\n");
                int rows = 0;
                for (int i = 0; i < configs.Count; i++)
                {
                    if (filter != null && filter.addedrows.ContainsKey(configs[i].key))
                        continue;
                    if(filter != null && filter.modifiedrows.ContainsKey(configs[i].key))
                        AppendConfig(ref sb, configs[i], filter.modifiedrows[configs[i].key]);
                    else
                        AppendConfig(ref sb, configs[i]);
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

            void AppendConfig(ref StringBuilder sb, config conf, tablerowdiff filter = null)
            {
                sb.Append("\t");
                sb.Append(conf.GenString(filter));
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
                    configs.Remove(configsDic[key]);
                    configsDic.Remove(key);
                }
            }

            void AddConfig(config cfg)
            {
                if (!configsDic.ContainsKey(cfg.key))
                {
                    configsDic[cfg.key] = cfg;
                    configs.Add(cfg);
                }
            }

            void ModifyConfig(config cfg)
            {
                if (configsDic.ContainsKey(cfg.key))
                {
                    oldCfg = configsDic[cfg.key];
                    int index = configs.IndexOf(configsDic[cfg.key]);
                    configs[index] = cfg;
                    configsDic[cfg.key] = cfg;
                }
            }
            #endregion

            #region 回退修改
            public void Cancel(string key)
            {
                //回退修改和删除
                if(oldCfg != null)
                {
                    int index = configs.IndexOf(configsDic[key]);
                    if(index < 0)
                        configs.Add(oldCfg);
                    else
                        configs[index] = oldCfg;
                    configsDic[key] = oldCfg;
                }
                else
                {
                    configs.Remove(configsDic[key]);
                    configsDic.Remove(key);
                }
                oldCfg = null;
            }
            #endregion
        }

        public class config
        {
            public string key;
            public List<property> properties = new List<property>();
            public Dictionary<string, property> propertiesDic = new Dictionary<string, property>();

            public config(string k)
            {
                key = k;
            }

            public string GenString(tablerowdiff filter = null)
            {
                StringBuilder sb = new StringBuilder(string.Format(configformat, key));
                int cells = 0;
                for(int i = 0; i < properties.Count; i++)
                {
                    if (filter != null && filter.deletedcells.ContainsKey(properties[i].name))
                        continue;
                    if(filter != null && filter.modifiedcells.ContainsKey(properties[i].name))
                        AppendProperty(ref sb, properties[i], filter.modifiedcells[properties[i].name].value);
                    else
                        AppendProperty(ref sb, properties[i]);
                    cells++;
                }
                if (filter != null && filter.addedcells.Count > 0)
                {
                    foreach (var p in filter.addedcells.Values)
                        AppendProperty(ref sb, p);
                    cells++;
                }
                if (cells > 0)//删除最后一行的逗号和空格 ", "
                    sb.Remove(sb.Length - 2, 2);
                sb.Append("}");
                return sb.ToString();
            }

            void AppendProperty(ref StringBuilder sb, property p, string newvalue = null)
            {
                sb.Append(p.GenString(newvalue));
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
                }
            }

            void AddProperty(property p)
            {
                if (!propertiesDic.ContainsKey(p.name))
                {
                    propertiesDic[p.name] = p;
                    properties.Add(p);
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
        }

        public class property
        {
            public string name;
            public string value;

            public string GenString(string newvalue = null)
            {
                if(newvalue == null)
                    return string.Format(kvformat, name, value);
                return string.Format(kvformat, name, newvalue);
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

        static property read_property(StreamReader sr)
        {
            property p = new property();
            p.name = llex_lite.buff2str();/* read key */
            llex_lite.llex(sr); p.value = llex_lite.buff2str();/* read val */
            return p;
        }

        static config read_config(StreamReader sr)
        {
            //llex_lite.llex(sr); /* read key */
            config config = new config(llex_lite.buff2str());/* read key */
            llex_lite.llex(sr, true);/* skip '{' */
            string k, v = null;
            while (!sr.EndOfStream && llex_lite.llex(sr) != '}')
            {
                property p = read_property(sr);
                config.properties.Add(p);
                config.propertiesDic.Add(p.name, p);
            }
            return config;
        }

        static table read_table(StreamReader sr)
        {
            string md5Str = read_md5comment(sr);
            llex_lite.llex(sr); /* read key */
            table t = new table(md5Str, llex_lite.buff2str());
            llex_lite.llex(sr, true);/* skip '{' */
            while(!sr.EndOfStream && llex_lite.llex(sr) != '}')
            {
                config conf = read_config(sr);
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

        static void read_file(string path)
        {
            StreamReader sr = new StreamReader(path);
            table t = read_table(sr);
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

        public static table parse(string path)
        {
            using (StreamReader sr = new StreamReader(path))
            {
                return read_table(sr);
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
