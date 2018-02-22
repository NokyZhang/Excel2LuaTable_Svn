
using System;
using System.Collections.Generic;
using System.ComponentModel;
using static Lua.lparser;

namespace ExcelTools.Scripts.UI
{
    class IDListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public string ID { get; set; }

        public string IdDisplay { get; set; }

        // 生成用于显示的IdDisplay
        // 各个表格的字段命名不同，此函数中设置了优先级,
        // NameZh > NameEn > Name (大小写不敏感)
        public static string GenIdDisplay(config cfg)
        {
            string str;
            string dis;
            #region 临时存储一个大小写不敏感的字典
            Dictionary<string, property> tmpPropertiesDic = new Dictionary<string, property>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, property> kv in cfg.propertiesDic)
            {
                if(!tmpPropertiesDic.ContainsKey(kv.Key))
                    tmpPropertiesDic.Add(kv.Key, kv.Value);
            }
            #endregion
            if (tmpPropertiesDic.ContainsKey("NameZh"))
            {
                dis = tmpPropertiesDic["NameZh"].value;
            }
            else if (tmpPropertiesDic.ContainsKey("NameEn"))
            {
                dis = tmpPropertiesDic["NameEn"].value;
            }
            else if (tmpPropertiesDic.ContainsKey("Name"))
            {
                dis = tmpPropertiesDic["Name"].value;
            }
            else
            {
                dis = "";
            }
            str = dis == ""? cfg.key : cfg.key + "（" + dis + "）";
            return str;
        }

        public int Row { get; set; }

        private List<string> _States;
        public List<string> States
        {
            get { return _States; }
            set
            {
                _States = value;
                Trunk_State = value[0];
                Studio_State = value[1];
                TF_State = value[2];
                Release_State = value[3];
            }
        }

        public void SetStates(string state, int branchIdx)
        {
            States[branchIdx] = state;
            switch (branchIdx)
            {
                case 0:
                    Trunk_State = state;
                    break;
                case 1:
                    Studio_State = state;
                    break;
                case 2:
                    TF_State = state;
                    break;
                case 3:
                    Release_State = state;
                    break;
            }
        }

        private string _Trunk_State;
        public string Trunk_State
        {
            get { return _Trunk_State; }
            set {
                    _Trunk_State = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Trunk_State"));
            }
        }

        private string _Studio_State;
        public string Studio_State
        {
            get { return _Studio_State; }
            set
            {
                _Studio_State = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Studio_State"));
            }
        }

        private string _TF_State;
        public string TF_State
        {
            get { return _TF_State; }
            set
            {
                _TF_State = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TF_State"));
            }
        }

        private string _Release_State;
        public string Release_State
        {
            get { return _Release_State; }
            set
            {
                _Release_State = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Release_State"));
            }
        }

        public List<bool> IsApplys { get; set; }
    }
}
