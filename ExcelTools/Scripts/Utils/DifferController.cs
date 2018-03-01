using System.IO;
using System.Collections.Generic;
using System.Linq;
using NPOI.XSSF.UserModel;
using NPOI.SS.UserModel;
using System.Collections.ObjectModel;
using ExcelTools.Scripts.UI;
using System.ComponentModel;
using System.Text;
using System.Windows;
using Lua;
using static Lua.lparser;

namespace ExcelTools.Scripts.Utils
{
    class DifferController
    {
        public const string STATUS_NONE = "";
        public const string STATUS_ADDED = "A";
        public const string STATUS_DELETED = "D";
        public const string STATUS_MODIFIED = "M";

        private static void AddModifiedRow(string rowkey, property property, int type, ref tablediff tdiff)
        {
            if (!tdiff.modifiedrows.ContainsKey(rowkey))
                tdiff.modifiedrows.Add(rowkey, new tablerowdiff());
            switch(type)
            {
                case 0://deleted
                    tdiff.modifiedrows[rowkey].deletedcells.Add(property.name, property);
                    break;
                case 1://added
                    tdiff.modifiedrows[rowkey].addedcells.Add(property.name, property);
                    break;
                case 2://modified
                    tdiff.modifiedrows[rowkey].modifiedcells.Add(property.name, property);
                    break;
            }
        }

        private static void CompareTablerow(config left, config right, ref tablediff tdiff)
        {
            for (int i = 0; i < right.properties.Count; i++)
            {
                if (!left.propertiesDic.ContainsKey(right.properties[i].name))
                    AddModifiedRow(right.key, right.properties[i], 0, ref tdiff);
                else if (!left.propertiesDic[right.properties[i].name].value.Equals(right.properties[i].value))
                    AddModifiedRow(right.key, right.properties[i], 2, ref tdiff);
            }
            foreach(var item in left.propertiesDic)
                if(!right.propertiesDic.ContainsKey(item.Key))
                    AddModifiedRow(left.key, item.Value, 1, ref tdiff);
        }

        public static tablediff CompareTable(table left, table right)
        {
            tablediff tdiff = new tablediff();
            if (left != null && right != null)
            {
                for (int i = 0; i < right.configs.Count; i++)
                {
                    if (left.configsDic.ContainsKey(right.configs[i].key))
                        CompareTablerow(left.configsDic[right.configs[i].key], right.configs[i], ref tdiff);
                    else
                        tdiff.deletedrows.Add(right.configs[i].key, right.configs[i]);
                }
            }
            if(left != null)
                foreach (var item in left.configsDic)
                    if(right == null || !right.configsDic.ContainsKey(item.Key))
                        tdiff.addedrows.Add(item.Key, item.Value);
            return tdiff;
        }
    }

    public class tablerowdiff
    {
        public Dictionary<string, property> addedcells = new Dictionary<string, property>();
        public Dictionary<string, property> deletedcells = new Dictionary<string, property>();
        public Dictionary<string, property> modifiedcells = new Dictionary<string, property>();

        public void Apply(string status, string key)
        {
            switch (status)
            {
                case DifferController.STATUS_ADDED:
                    addedcells.Remove(key);
                    break;
                case DifferController.STATUS_DELETED:
                    deletedcells.Remove(key);
                    break;
                case DifferController.STATUS_MODIFIED:
                    modifiedcells.Remove(key);
                    break;
                default: break;
            }
        }
    }

    public class tablediff
    {
        public Dictionary<string, config> addedrows = new Dictionary<string, config>();
        public Dictionary<string, config> deletedrows = new Dictionary<string, config>();
        public Dictionary<string, tablerowdiff> modifiedrows = new Dictionary<string, tablerowdiff>();

        //仅仅用于回退
        private Dictionary<string, tablerowdiff> modifiedrowsAppled = new Dictionary<string, tablerowdiff>();
        private Dictionary<string, config> addedrowsAppled = new Dictionary<string, config>();
        private Dictionary<string, config> deletedrowsAppled = new Dictionary<string, config>();

        public void Apply(string status, string key, table bt, table lt)
        {
            config cfg;
            switch (status)
            {
                case DifferController.STATUS_ADDED:
                    addedrowsAppled.Add(key, addedrows[key]);
                    addedrows.Remove(key);
                    cfg = lt.configsDic[key];
                    bt.Apply(status, cfg);
                    break;
                case DifferController.STATUS_DELETED:
                    deletedrowsAppled.Add(key, deletedrows[key]);
                    deletedrows.Remove(key);
                    bt.Apply(status, null, key);
                    break;
                case DifferController.STATUS_MODIFIED:
                    modifiedrowsAppled.Add(key, modifiedrows[key]);
                    modifiedrows.Remove(key);
                    cfg = lt.configsDic[key];
                    bt.Apply(status, cfg);
                    break;
                default: break;
            }
        }
        public void Cancel(string key, table bt)
        {
            if (modifiedrowsAppled.ContainsKey(key))
            {
                modifiedrows.Add(key, modifiedrowsAppled[key]);
                modifiedrowsAppled.Remove(key);
            }
            else if(deletedrowsAppled.ContainsKey(key))
            {
                deletedrows.Add(key, deletedrowsAppled[key]);
                deletedrowsAppled.Remove(key);
            }
            else if(addedrowsAppled.ContainsKey(key))
            {
                addedrows.Add(key, addedrowsAppled[key]);
                addedrowsAppled.Remove(key);
            }
                //table结构的回退
                bt.Cancel(key);
            }
        }
    }
