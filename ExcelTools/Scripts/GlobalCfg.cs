using ExcelTools.Scripts.UI;
using ExcelTools.Scripts.Utils;
using Lua;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using static ExcelTools.Scripts.Utils.DifferController;
using static Lua.lparser;

namespace ExcelTools.Scripts
{
    class LuaTableData
    {
        public List<table> tables;
        public List<tablediff> tableDiffs;
        public ObservableCollection<IDListItem> idList;

        public Dictionary<string, Dictionary<int, string>> applyedRows;

        public void ModifiedTables(int index, table val)
        {
            if (tables.Count > index)
                tables[index] = val;
            else
                tables.Add(val);
        }

        public bool IsInitTable(int index)
        {
            if (tables.Count > index)
                return true;
            else
                return false;
        }
    }

    class GlobalCfg
    {
        //表格的路径
        static public string SourcePath = null;
        static public string LocalTmpTablePath = "../TmpTable/Local/";
        //现处于管理中的分支
        static public List<string> BranchURLs = new List<string>()
        {
            "svn://svn.sg.xindong.com/RO/client-trunk",
            "svn://svn.sg.xindong.com/RO/client-branches/Studio",
            "svn://svn.sg.xindong.com/RO/client-branches/TF",
            "svn://svn.sg.xindong.com/RO/client-branches/Release"
        };
        static public List<string> TmpTableRelativePaths = new List<string>()
        {
            "../TmpTable/Trunk/",
            "../TmpTable/Studio/",
            "../TmpTable/TF/",
            "../TmpTable/Release/"
        };

        static public List<string> TmpTableRealPaths;       

        static public int BranchCount { get { return BranchURLs.Count; } }

        static public bool isServer = true;

        static public string _Local_Table_Ext = ".txt";
        private static GlobalCfg _instance;

        public static GlobalCfg Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new GlobalCfg();
                return _instance;
            }
        }

        private GlobalCfg()
        {
            _ExcelDic = new Dictionary<string, Excel>();
            _lTableDataDic = new Dictionary<string, LuaTableData>();
            LockedPaths = new List<string>();
        }

        //所有表格的解析都存在这里
        //TODO：之后可能用多线程提早个别表格的解析操作，优化操作体验
        private Dictionary<string, Excel> _ExcelDic;

        private Dictionary<string, LuaTableData> _lTableDataDic;

        public List<string> LockedPaths;

        public Excel GetParsedExcel(string path, bool reParse = false)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            if (!_ExcelDic.ContainsKey(path) || reParse)
            {
                _ExcelDic[path] = Excel.Parse(path, isServer);
            }
            return _ExcelDic[path];
        }

        public void ClearAll()
        {
            _ExcelDic.Clear();
            _lTableDataDic.Clear();
        }

        public void ClearCurrent()
        {
            if (_lTableDataDic.ContainsKey(currentExcelpath))
            {
                _lTableDataDic[currentExcelpath].tables.Clear();
                _lTableDataDic[currentExcelpath].tableDiffs.Clear();
                _lTableDataDic[currentExcelpath].idList = null;
            }
        }

        private LuaTableData InitLuaTableData(string excelpath)
        {
            if (!_lTableDataDic.ContainsKey(excelpath))
            {
                _lTableDataDic[excelpath] = new LuaTableData
                {
                    tables = new List<table>(),
                    tableDiffs = new List<tablediff>()
                };
            }
            //对比md5，看是否需要重新生成LocalLuaTable llt
            string tablename = string.Format("Table_{0}", Path.GetFileNameWithoutExtension(excelpath));
            string lltpath = Path.Combine(SourcePath, LocalTmpTablePath, tablename + ".txt");
            string md5 = ExcelParserFileHelper.GetMD5HashFromFile(excelpath);
            LuaTableData ltd = _lTableDataDic[excelpath];
            if (!File.Exists(lltpath) || md5 != ReadTableMD5(lltpath))
            {
                ExcelParser.ReGenLuaTable(excelpath);
            }
            if (!ltd.IsInitTable(0))
            {
                ltd.ModifiedTables(0, parse(lltpath));
            }
            TmpTableRealPaths = GenTmpPath(tablename);
            for (int i = 1; i < TmpTableRealPaths.Count + 1; i++)
            {
                if (File.Exists(TmpTableRealPaths[i - 1]))
                {
                    if (!ltd.IsInitTable(i))
                        ltd.ModifiedTables(i, parse(TmpTableRealPaths[i - 1]));
                }
                else
                {
                    if (!ltd.IsInitTable(i))
                        ltd.ModifiedTables(i, null);
                }
            }
            return ltd;
        }

        #region UI数据相关
        LuaTableData currentLuaTableData;
        string currentExcelpath;

        public List<string> GetRowAllStatus(string rowid)
        {
            List<string> status = new List<string>();
            for (int i = 0; i < BranchURLs.Count; i++)
            {
                if (currentLuaTableData.tableDiffs.Count > i && currentLuaTableData.tableDiffs[i] != null)
                {
                    if (currentLuaTableData.tableDiffs[i].addedrows.ContainsKey(rowid))
                        status.Add(DifferController.STATUS_ADDED);
                    else if (currentLuaTableData.tableDiffs[i].deletedrows.ContainsKey(rowid))
                        status.Add(DifferController.STATUS_DELETED);
                    else if (currentLuaTableData.tableDiffs[i].modifiedrows.ContainsKey(rowid))
                        status.Add(DifferController.STATUS_MODIFIED);
                    else
                        status.Add(DifferController.STATUS_NONE);
                }
                else
                    status.Add(DifferController.STATUS_DELETED);
            }
            return status;
        }

        public tablerowdiff GetCellAllStatus(string rowid,int branchIdx)
        {
            if (!currentLuaTableData.tableDiffs[branchIdx].modifiedrows.ContainsKey(rowid))
                return null;
            tablerowdiff tablerowdiff = currentLuaTableData.tableDiffs[branchIdx].modifiedrows[rowid];
            return tablerowdiff;
        }

        private Dictionary<string, IDListItem> GetExcelDeletedRow()
        {
            Dictionary<string, IDListItem> tmpDic = new Dictionary<string, IDListItem>();
            for (int i = 0; i < currentLuaTableData.tableDiffs.Count; i++)
            {
                if (currentLuaTableData.tableDiffs[i] != null)
                {
                    foreach (var id in currentLuaTableData.tableDiffs[i].deletedrows.Keys)
                    {
                        if (!tmpDic.ContainsKey(id))
                        {
                            tmpDic.Add(id, new IDListItem
                            {
                                ID = id,
                                Row = -1,
                                States = new List<string>()
                            });
                            for (int k = 0; k < BranchCount; k++)//初始化状态为STATUS_NONE
                                tmpDic[id].States.Add(DifferController.STATUS_NONE);
                        }
                        tmpDic[id].States[i] = DifferController.STATUS_DELETED;
                    }
                }
            }
            return tmpDic;
        }

        public ref ObservableCollection<IDListItem> GetIDList(string excelpath)
        {
            currentLuaTableData = InitLuaTableData(excelpath);
            currentExcelpath = excelpath;
            if (currentLuaTableData.tableDiffs.Count <= 0)
            {
                for (int i = 1; i < currentLuaTableData.tables.Count; i++)
                {
                    currentLuaTableData.tableDiffs.Add(CompareTable(currentLuaTableData.tables[0], currentLuaTableData.tables[i]));
                }
            }
            if (currentLuaTableData.idList == null)
            {
                currentLuaTableData.idList = new ObservableCollection<IDListItem>();
                for (int i = 0; i < currentLuaTableData.tables[0].configs.Count; i++)
                {
                    currentLuaTableData.idList.Add(new IDListItem
                    {
                        ID = currentLuaTableData.tables[0].configs[i].key,
                        Row = i,
                        States = GetRowAllStatus(currentLuaTableData.tables[0].configs[i].key),
                        IsApplys = new List<bool>() { false, false, false, false }
                    });
                }
                Dictionary<string, IDListItem> tmpDic = GetExcelDeletedRow();
                foreach (var item in tmpDic.Values)
                    currentLuaTableData.idList.Add(item);
            }
            return ref currentLuaTableData.idList;
        }

        //仅修改逻辑缓存中的值，不直接修改配置文件
        public void ApplyRow(int branchIdx, IDListItem item)
        {
            //if(currentTablediffs.Count > branchIdx && currentTables.Count > branchIdx + 1 &&
            //    currentTablediffs[branchIdx] != null && currentTables[branchIdx + 1] != null &&
            //    item != null && item.States.Count > branchIdx)
            //{
            //}
            table lt = currentLuaTableData.tables[0];//local table
            table bt = currentLuaTableData.tables[branchIdx + 1];//branch table
            tablediff btd = currentLuaTableData.tableDiffs[branchIdx];//branch tablediff
            if (bt == null)
            {
                bt = new table(lt);
                currentLuaTableData.tables[branchIdx + 1] = bt;
            }

            string status = item.States[branchIdx];
            btd.Apply(status, item.ID, bt, lt);
            if(currentLuaTableData.applyedRows == null)
            {
                currentLuaTableData.applyedRows = new Dictionary<string, Dictionary<int, string>>();
            }
            if (!currentLuaTableData.applyedRows.ContainsKey(item.ID)) {
                currentLuaTableData.applyedRows[item.ID] = new Dictionary<int, string>();
            }
            currentLuaTableData.applyedRows[item.ID][branchIdx] = item.States[branchIdx];
            int index = currentLuaTableData.idList.IndexOf(item);
            _lTableDataDic[currentExcelpath].idList[index].SetStates(STATUS_NONE, branchIdx);
        }

        public void CancelRow(int branchIdx, IDListItem item)
        {
            Dictionary<string, Dictionary<int, string>> applyedRows = currentLuaTableData.applyedRows;
            table bt = currentLuaTableData.tables[branchIdx + 1];//branch table
            tablediff btd = currentLuaTableData.tableDiffs[branchIdx];//branch tablediff
            if (bt == null)
            {
                return;
            }
            btd.Cancel(item.ID, bt);
            int index = currentLuaTableData.idList.IndexOf(item);
            _lTableDataDic[currentExcelpath].idList[index].SetStates(applyedRows[item.ID][branchIdx], branchIdx);
            applyedRows[item.ID][branchIdx] = "";
        }

        #endregion

        //根据目前选择的操作，修改配置文件
        public void ExcuteModified(int branchIdx)
        {
            table bt = currentLuaTableData.tables[branchIdx + 1];//branch table
            tablediff btd = currentLuaTableData.tableDiffs[branchIdx];//branch tablediff
            string tmp = bt.GenString(null, btd);
            string aimTmpPath = TmpTableRealPaths[branchIdx];
            FileUtil.WriteTextFile(tmp, aimTmpPath);
        }

        public List<config> GetTableRow(string id)
        {
            List<config> rows = new List<config>();
            for (int i = 0; i < currentLuaTableData.tables.Count; i++)
            {
                if (currentLuaTableData.tables[i] != null && currentLuaTableData.tables[i].configsDic.ContainsKey(id))
                    rows.Add(currentLuaTableData.tables[i].configsDic[id]);
                else
                    rows.Add(null);
            }
            return rows;
        }

        //生成临时table的路径
        private static List<string> GenTmpPath(string tableName)
        {
            Directory.CreateDirectory(Path.Combine(SourcePath, TmpTableRelativePaths[0]));
            Directory.CreateDirectory(Path.Combine(SourcePath, TmpTableRelativePaths[1]));
            Directory.CreateDirectory(Path.Combine(SourcePath, TmpTableRelativePaths[2]));
            Directory.CreateDirectory(Path.Combine(SourcePath, TmpTableRelativePaths[3]));
            List<string> tmpFolders = new List<string>
            {
                Path.Combine(SourcePath, TmpTableRelativePaths[0], tableName) + _Local_Table_Ext,
                Path.Combine(SourcePath, TmpTableRelativePaths[1], tableName) + _Local_Table_Ext,
                Path.Combine(SourcePath, TmpTableRelativePaths[2], tableName) + _Local_Table_Ext,
                Path.Combine(SourcePath, TmpTableRelativePaths[3], tableName) + _Local_Table_Ext
            };
            return tmpFolders;
        }
    }
}
