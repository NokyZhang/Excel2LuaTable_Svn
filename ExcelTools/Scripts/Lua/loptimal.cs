using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lua.lparser;

namespace Lua
{
    class loptimal
    {
        enum Optimization
        {
            Normal,
            Group,
            Skill
        }

        static Setting _NormalSetting = new Setting(Optimization.Normal, null);
        struct Setting
        {
            public Optimization type;
            public List<string> EigenValues;

            public Setting(Optimization t, List<string> vs)
            {
                type = t;
                EigenValues = vs;
            }
        }

        #region 非Normal类型的LuaTable记录如下，并记录分组需要的特征值。
        static Dictionary<string, Setting> _OptimalSetting = new Dictionary<string, Setting>()
        {
            {"Table_Equip",
                new Setting(Optimization.Group, new List<string>()
                { "CanEquip", "Type", "EquipType" })
            },
            {"Table_Item",
                new Setting(Optimization.Group, new List<string>()
                { "Icon", "Type" })
            },
            {"Table_Monster",
                new Setting(Optimization.Group, new List<string>()
                { "Type", "Race", "Nature" })
            },
            {"Table_Npc",
                new Setting(Optimization.Group, new List<string>()
                { "Type", "Race", "Nature" })
            },
            {"Table_Dialog",
                new Setting(Optimization.Group, new List<string>()
                { "Text" })
            },
            {"Table_Skill", new Setting(Optimization.Skill, null) }
        };
        #endregion

        static Setting get_table_setting(table table)
        {
            Setting type = _NormalSetting;
            if (_OptimalSetting.ContainsKey(table.name))
                type = _OptimalSetting[table.name];
            return type;
        }

        public static void optimal(string path, string outpath)
        {
            table table = lparser.parse(path);
            Setting ts = get_table_setting(table);
            string ret = string.Empty;
            switch(ts.type)
            {
                case Optimization.Normal:
                    ret = optimal_normal(table);
                    break;
                case Optimization.Group:
                    ret = optimal_group(table, ts);
                    break;
                case Optimization.Skill:
                    ret = optimal_skill(table);
                    break;
            }

            FileUtil.OverWriteText(outpath, ret);
        }

        #region Normal优化，即只抽取公共字段，将重复次数最多的值作为默认值

        /// <summary>
        /// 以LuaTable的第一条配置的所有属性作为base
        /// </summary>
        /// <param name="configs"></param>
        /// <returns></returns>
        static Dictionary<string, Dictionary<string, int>> get_base_properties(List<config> configs)
        {
            Dictionary<string, Dictionary<string, int>> basedic = new Dictionary<string, Dictionary<string, int>>();
            if (configs.Count > 0)
            {
                config baseconfig = configs[0];
                for (int i = 0; i < baseconfig.properties.Count; i++)
                {
                    //id字段不能成为基础属性，因为没有复用的必要
                    if(baseconfig.properties[i].name != "id")
                        basedic.Add(baseconfig.properties[i].name, new Dictionary<string, int>{ { baseconfig.properties[i].value, 1 } });
                }
            }
            return basedic;
        }

        static Dictionary<string, string> count_baseKVs(Dictionary<string, Dictionary<string, int>> basedic)
        {
            Dictionary<string, string> basekv = new Dictionary<string, string>();
            string val = null;
            int count = 1;
            foreach(var item in basedic)
            {
                foreach(var vs in item.Value)
                {
                    if(vs.Value > count)
                    {
                        val = vs.Key;
                        count = vs.Value;
                    }
                }
                if (val != null)
                    basekv.Add(item.Key, val);
                else
                    basekv.Remove(item.Key);
                val = null; count = 1;
            }
            return basekv;
        }

        static void swap_properties_base(ref Dictionary<string, Dictionary<string, int>> o, ref Dictionary<string, Dictionary<string, int>> n)
        {
            Dictionary<string, Dictionary<string, int>> temp = o;
            o = n;
            temp.Clear();
            n = temp;
        }

        //static string gen_metatable(Dictionary<string, Dictionary<string, int>> basedic)

        static string gen_normal(table table, Dictionary<string, string> basekv)
        {
            //去除不需要生成的属性
            property temp;
            for (int i = 0; i < table.configs.Count; i++)
            {
                for (int j = 0; j < table.configs[i].properties.Count; j++)
                {
                    temp = table.configs[i].properties[j];
                    if (basekv.ContainsKey(temp.name) && basekv[temp.name] == temp.value)
                        table.configs[i].properties.RemoveAt(j--);
                }
            }

            Func<string> genmeta = () =>
            {
                string meta = "local __default = {\n";
                foreach(var item in basekv)
                    meta = string.Format("{0}\t{1} = {2},\n", meta, item.Key, item.Value);
                meta = string.Format("{0}}}\ndo\n\tlocal base = {{\n\t\t__index = __default,\n\t\t--__newindex = function()\n\t\t\t--禁止写入新值\n\t\t\t--error(\"Attempt to modify read-only table\")\n\t\t--end\n\t}}\n\tfor k, v in pairs({1}) do\n\t\tsetmetatable(v, base)\n\tend\n\tbase.__metatable = false\nend\n", meta, table.name);
                return meta;
            };
            return table.GenString(genmeta);
        }

        static Dictionary<string, string> get_basekvs(List<config> configs)
        {
            Dictionary<string, Dictionary<string, int>> basedic = get_base_properties(configs);
            Dictionary<string, Dictionary<string, int>> newbase = new Dictionary<string, Dictionary<string, int>>();
            property temp;
            //configs在构造时即为 new List<string>()，一定不为空
            for (int i = 1; i < configs.Count; i++)
            {
                for (int j = 0; j < configs[i].properties.Count; j++)
                {
                    temp = configs[i].properties[j];
                    if (basedic.ContainsKey(temp.name))
                    {
                        newbase.Add(temp.name, basedic[temp.name]);
                        if (newbase[temp.name].ContainsKey(temp.value))
                            newbase[temp.name][temp.value]++;
                        else
                            newbase[temp.name].Add(temp.value, 1);
                    }
                }
                swap_properties_base(ref basedic, ref newbase);
            }
            return count_baseKVs(basedic);
        }

        static string optimal_normal(table table)
        {
            return gen_normal(table, get_basekvs(table.configs));
        }
        #endregion

        #region Group优化，即分组优化，自动将特征值一致的配置分为同一组。然后抽取相同的值，作为元表。

        static List<List<config>> partition_grop(table table, List<string> eigenvalues)
        {
            Dictionary<string, List<config>> group = new Dictionary<string, List<config>>();
            int total = eigenvalues.Count;
            string[] strs = new string[total];
            int idx;
            for(int i = 0; i < table.configs.Count; i++)
            {
                for(int j = 0; j < table.configs[i].properties.Count; j++)
                {
                    idx = eigenvalues.IndexOf(table.configs[i].properties[j].name);
                    if(idx > -1)
                    {
                        strs[idx] = table.configs[i].properties[j].value;
                        total--;
                    }
                }
                if (total == 0)
                {
                    string key = string.Join(string.Empty, strs);
                    if (group.ContainsKey(key))
                        group[key].Add(table.configs[i]);
                    else
                        group.Add(key, new List<config> { table.configs[i] });
                }
                total = eigenvalues.Count;
            }
            List<List<config>> grouplist = new List<List<config>>();
            foreach (var list in group.Values)
            {
                if(list.Count > 1)
                    grouplist.Add(list);
            }
            return grouplist;
        }

        /// <summary>
        /// key为一组中第一个被记录下的那个值，使用这个值便于使用时查找。
        /// </summary>
        /// <param name="basekvs"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        static string gen_baseconf_metatable(Dictionary<string, string> basekvs, string key)
        {
            StringBuilder sb = new StringBuilder(string.Format("[{0}] = {{", key));
            int n = basekvs.Count;
            foreach(var item in basekvs)
            {
                n--;
                sb.AppendFormat("{0} = {1}", item.Key, item.Value);
                if (n > 0)
                    sb.Append(", ");
            }
            sb.Append("}");
            return sb.ToString();
        }

        static string gen_grouponf_metatable(List<config> configs)
        {
            StringBuilder sb = new StringBuilder("{");
            for(int i = 0; i < configs.Count; i++)
            {
                sb.Append(configs[i].key);
                if (i < configs.Count - 1)
                    sb.Append(", ");
            }
            sb.Append("}");
            return sb.ToString();
        }

        static void build_datamap(List<List<config>> group, out Dictionary<string, Dictionary<string, string>> basedic, out Dictionary<List<config>, Dictionary<string, string>> groupdic)
        {
            basedic = new Dictionary<string, Dictionary<string, string>>();
            groupdic = new Dictionary<List<config>, Dictionary<string, string>>();
            for (int i = 0; i < group.Count; i++)
            {
                List<config> list = group[i];
                if (list.Count > 1)
                {
                    Dictionary<string, string> basekvs = get_basekvs(list);
                    if (basekvs.Count > 0)
                    {
                        for (int j = 0; j < list.Count; j++)
                        {
                            if (basedic.ContainsKey(list[j].key))
                                Console.Error.WriteLine("已存在的key = " + list[j].key);
                            else
                                basedic.Add(list[j].key, basekvs);
                        }
                        groupdic.Add(list, basekvs);
                    }
                }
            }
        }

        static void table_deduplication(ref table table, Dictionary<string, Dictionary<string, string>> basedic)
        {
            if (basedic.Count < 1)
                return;
            property temp;
            Dictionary<string, string> basekv = new Dictionary<string, string>();
            for (int i = 0; i < table.configs.Count; i++)
            {
                if (basedic.ContainsKey(table.configs[i].key))
                {
                    basekv = basedic[table.configs[i].key];
                    for (int j = 0; j < table.configs[i].properties.Count; j++)
                    {
                        temp = table.configs[i].properties[j];
                        if (basekv.ContainsKey(temp.name) && basekv[temp.name] == temp.value)
                            table.configs[i].properties.RemoveAt(j--);
                    }
                }
            }
        }

        static string gen_group_metatable(Dictionary<List<config>, Dictionary<string, string>> groupdic, string tablename)
        {
            StringBuilder __base = new StringBuilder("local __base = {\n");
            StringBuilder __groups = new StringBuilder("local groups = {\n");
            int n = groupdic.Count;
            foreach (var item in groupdic)
            {
                n--;
                __groups.Append("\t");
                __base.Append("\t");
                __groups.Append(gen_grouponf_metatable(item.Key));
                __base.Append(gen_baseconf_metatable(item.Value, item.Key[0].key));
                if (n > 0)
                {
                    __groups.Append(",\n");
                    __base.Append(",\n");
                }
                else
                {
                    __groups.Append("\n");
                    __base.Append("\n");
                }
            }
            __base.Append("}\nfor _,v in pairs(__base) do\n\tv.__index = v\nend\n");
            __groups.AppendFormat("}}\nfor i=1, #groups do\n\tfor j=1, #groups[i] do\n\t\tsetmetatable({0}[groups[i][j]], __base[groups[i][1]])\n\tend\nend\n", tablename);
            return __base.ToString() + __groups.ToString();
        }

        static string gen_group(table table, List<List<config>> group)
        {
            Dictionary<string, Dictionary<string, string>> basedic;
            Dictionary<List<config>, Dictionary<string, string>> groupdic;
            build_datamap(group, out basedic, out groupdic);

            //去除不需要生成的属性
            table_deduplication(ref table, basedic);

            Func<string> genmeta = () =>
            {
                return gen_group_metatable(groupdic, table.name);
            };
            return table.GenString(genmeta);
        }

        static string optimal_group(table table, Setting setting)
        {
            List<List<config>> group = partition_grop(table, setting.EigenValues);
            return gen_group(table, group);
        }
        #endregion

        #region Skill优化，即特殊化的Group优化。不是以特征值分组，而是以NextID链式的配置分为一组。

        struct SkillGroupNode
        {
            public string nextid;
            public config config;
        }

        static string get_nextid(config conf)
        {
            for(int i = 0; i < conf.properties.Count; i++)
            {
                if (conf.properties[i].name == "NextID" || conf.properties[i].name == "NextBreakID")
                    return conf.properties[i].value;
            }
            return null;
        }

        static void insert_node(ref List<List<SkillGroupNode>> lists, List<SkillGroupNode> nodes)
        {
            //防止链表循环
            //如果循环，则配置出现严重失误，配置了一个技能升级闭环
            Debug.Assert(nodes.Last().nextid != nodes[0].config.key);
            for (int i = 0; i < lists.Count; i++)
            {
                SkillGroupNode head = lists[i][0];
                SkillGroupNode last = lists[i][lists[i].Count - 1];
                if (head.config.key == nodes.Last().nextid)
                {
                    lists[i].InsertRange(0, nodes);
                    lists.Remove(nodes);
                    insert_node(ref lists, lists[i]);
                }
                else if (last.nextid == nodes[0].config.key)
                {
                    lists[i].AddRange(nodes);
                    lists.Remove(nodes);
                    insert_node(ref lists, lists[i]);
                }
            }
        }

        static void insert_node(ref List<List<SkillGroupNode>> lists, SkillGroupNode node)
        {
            for(int i = 0; i < lists.Count; i++)
            {
                SkillGroupNode head = lists[i][0];
                SkillGroupNode last = lists[i][lists[i].Count - 1];
                if (head.config.key == node.nextid)
                {
                    lists[i].Insert(0, node);
                    insert_node(ref lists, lists[i]);
                    return;
                }
                else if (last.nextid == node.config.key)
                {
                    lists[i].Add(node);
                    insert_node(ref lists, lists[i]);
                    return;
                }
            }
            lists.Add(new List<SkillGroupNode> { node });
        }

        static List<List<config>> build_group(table table)
        {
            List<List<SkillGroupNode>> lists = new List<List<SkillGroupNode>>();
            for (int i = 0; i < table.configs.Count; i++)
            {
                string _Nextid = get_nextid(table.configs[i]);
                if (_Nextid != null)
                {
                    SkillGroupNode node = new SkillGroupNode
                    {
                        nextid = _Nextid,
                        config = table.configs[i]

                    };
                    insert_node(ref lists, node);
                }
            }
            List<List<config>> group = new List<List<config>>();
            for(int i = 0; i < lists.Count; i++)
            {
                group.Add(new List<config>());
                for(int j = 0; j < lists[i].Count; j++)
                    group[i].Add(lists[i][j].config);
            }
            return group;
        }

        static List<List<config>> partition_grop(table table)
        {
            return build_group(table);
        }

        static string optimal_skill(table table)
        {
            List<List<config>> group = partition_grop(table);
            return gen_group(table, group);
        }
        #endregion
    }
}
