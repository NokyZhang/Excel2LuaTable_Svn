using ExcelTools.Scripts.UI;
using ExcelTools.Scripts.Utils;
using Lua;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using static ExcelTools.Scripts.Utils.DifferController;
using static Lua.lparser;

namespace ExcelTools.Scripts
{
    class LuaTableData
    {
        public table[] tables = new table[GlobalCfg.BranchCount + 1];
        public List<tablediff> tableDiffs;
        public ObservableCollection<IDListItem> idList;

        public Dictionary<string, Dictionary<int, string>> applyedRows;

        public void SetIsNeedGen(string cfgId, string propertyName, bool isNeedGen)
        {
            for(int i = 0; i < tables.Length; i++)
            {
                if (tables[i].configsDic.ContainsKey(cfgId))
                {
                    tables[i].configsDic[cfgId].SetIsNeedGen(propertyName, isNeedGen);
                }
            }
        }

        public void ResetIsNeedGen()
        {
            for(int i = 0; i < tables.Length; i++)
            {
                for(int j = 0; j < tables[i].configs.Count; j++)
                {
                    for (int n = 0; n < tables[i].configs[j].properties.Count; n++)
                    {
                        tables[i].configs[j].IsNeedGenDic[tables[i].configs[j].properties[n].name] = true;
                    }
                }
            }
        }
    }

     enum Branch
    {
        Trunk = 0,
        Studio = 1,
        TF = 2,
        Release = 3,
    }

    class GlobalCfg
    {

        //表格的路径
        static public string SourcePath = null;
        //现处于管理中的分支
        static public List<string> BranchURLs = new List<string>()
        {
            "svn://svn.sg.xindong.com/RO/client-trunk",
            "svn://svn.sg.xindong.com/RO/client-branches/Studio",
            "svn://svn.sg.xindong.com/RO/client-branches/TF",
            "svn://svn.sg.xindong.com/RO/client-branches/Release"
        };
        const string LuaLocalPath = "../TmpTable/Local/";
        static public List<string> LuaTablePaths = new List<string>()
        {
            "../TmpTable/Trunk/",
            "../TmpTable/Studio/",
            "../TmpTable/TF/",
            "../TmpTable/Release/"
        };

        static public int BranchCount { get { return BranchURLs.Count; } }

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
            if (!_ExcelDic.ContainsKey(path) || reParse || _ExcelDic[path] == null)
            {
                _ExcelDic[path] = Excel.Parse(path);
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
                _lTableDataDic.Remove(currentExcelpath);
            }
        }

        private LuaTableData InitLuaTableData(string excelpath)
        {
            if (!_lTableDataDic.ContainsKey(excelpath))
            {
                _lTableDataDic[excelpath] = new LuaTableData
                {
                    tableDiffs = new List<tablediff>()
                };
                //对比md5，看是否需要重新生成LocalLuaTable llt
                string tablename = string.Format("Table_{0}", Path.GetFileNameWithoutExtension(excelpath));
                string slltpath = GetLocalServerLuaPath(tablename);
                string clltpath = GetLocalClientLuaPath(excelpath);
                string md5 = ExcelParserFileHelper.GetMD5HashFromFile(excelpath);
                bool isServer = ExcelParserFileHelper.IsServer(excelpath);
                if (md5 == null)
                {
                    return null;
                }
                LuaTableData ltd = _lTableDataDic[excelpath];
                if (!File.Exists(slltpath) || md5 != ReadTableMD5(slltpath)
                    || (!isServer && (!File.Exists(clltpath) || md5 != ReadTableMD5(clltpath))))
                {
                    if (!ExcelParser.ReGenLuaTable(excelpath))
                    {
                        return null;
                    }
                }
                ltd.tables[0] = parse(slltpath, excelpath);
                for (int i = 1; i < BranchCount + 1; i++)
                {
                    string serverLuaPath = GetBranchServerLuaPath(tablename, i - 1);
                    if (File.Exists(serverLuaPath))
                        ltd.tables[i] = parse(serverLuaPath, excelpath);
                    else
                        ltd.tables[i] = null;
                }
            }
            return _lTableDataDic[excelpath];
        }

        private LuaTableData previousLuaTableData;
        private LuaTableData currentLuaTableData;
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
                        List<string> states = new List<string>();
                        for (int k = 0; k < BranchCount; k++)
                        {
                            states.Add(DifferController.STATUS_NONE);
                        }
                        if (!tmpDic.ContainsKey(id))
                        {
                            tmpDic.Add(id, new IDListItem
                            {
                                ID = id,
                                IdDisplay = id,
                                Row = -1,
                                States = states,
                            });
                        }
                        tmpDic[id].SetStates(DifferController.STATUS_DELETED, i);
                    }
                }
            }
            return tmpDic;
        }

        public  ObservableCollection<IDListItem> GetIDList(string excelpath)
        {
            previousLuaTableData = currentLuaTableData;
            currentLuaTableData = InitLuaTableData(excelpath);
            if(currentLuaTableData == null)
            {
                return null;
            }
            currentExcelpath = excelpath;
            if (currentLuaTableData.tableDiffs.Count <= 0)
            {
                for (int i = 1; i < currentLuaTableData.tables.Length; i++)
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
                        IdDisplay = IDListItem.GenIdDisplay(currentLuaTableData.tables[0].configs[i]),
                        Row = i,
                        States = GetRowAllStatus(currentLuaTableData.tables[0].configs[i].key),
                    });
                }
                Dictionary<string, IDListItem> tmpDic = GetExcelDeletedRow();
                foreach (var item in tmpDic.Values)
                    currentLuaTableData.idList.Add(item);
            }
            return currentLuaTableData.idList;
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

        //根据目前选择的操作，修改配置文件
        public void ExcuteModified(int branchIdx)
        {
            table bt = currentLuaTableData.tables[branchIdx + 1];
            string serverContent = bt.GenString(null, true);
            string clientContent = bt.GenString(null, false);
            string serverAimTmpPath = GetBranchServerLuaPath(bt.name, branchIdx);
            string clientAimTmpPath = GetBranchClientLuaPath(currentExcelpath, branchIdx);
            FileUtil.WriteTextFile(serverContent, serverAimTmpPath);
            FileUtil.WriteTextFile(clientContent, clientAimTmpPath);
            //loptimal.optimal(aimTmpPath, );
            Instance.ClearCurrent();
        }

        public List<config> GetTableRow(string id)
        {
            List<config> rows = new List<config>();
            for (int i = 0; i < currentLuaTableData.tables.Length; i++)
            {
                if (currentLuaTableData.tables[i] != null && currentLuaTableData.tables[i].configsDic.ContainsKey(id))
                    rows.Add(currentLuaTableData.tables[i].configsDic[id]);
                else
                    rows.Add(null);
            }
            return rows;
        }

        public bool GetIsNeedGen(string cfgId, string propertyName)
        {
            bool isNeedGen = true;
            for (int i = 0; i < currentLuaTableData.tables.Length; i++)
            {
                if (currentLuaTableData.tables[i] != null
                    && currentLuaTableData.tables[i].configsDic.ContainsKey(cfgId)
                    && currentLuaTableData.tables[i].configsDic[cfgId].IsNeedGenDic.ContainsKey(propertyName))
                {
                    isNeedGen = currentLuaTableData.tables[i].configsDic[cfgId].IsNeedGenDic[propertyName];
                }
            }
            return isNeedGen;
        }

        public void SetCurrentIsNeedGen(string cfgId, string propertyName, bool isNeedGen)
        {
            currentLuaTableData.SetIsNeedGen(cfgId, propertyName, isNeedGen);
        }

        public void ResetPreviousIsNeedGen()
        {
            if(previousLuaTableData != null)
            {
                previousLuaTableData.ResetIsNeedGen();
            }
        }

        public static string GetBranchServerLuaPath(string tableName, int branchId)
        {
            Directory.CreateDirectory(Path.Combine(SourcePath, LuaTablePaths[branchId]));
            return Path.Combine(SourcePath, LuaTablePaths[branchId], tableName) + _Local_Table_Ext;
        }

        public static string GetBranchClientLuaPath(string excelPath, int branchId)
        {
            string clientLuaPath = ExcelParserFileHelper.GetTempLuaPath(excelPath, false);
            clientLuaPath = clientLuaPath.Replace("/tmp/", LuaTablePaths[branchId].Substring(2));
            return clientLuaPath;
        }

        public static string GetLocalServerLuaPath(string tableName)
        {
            Directory.CreateDirectory(Path.Combine(SourcePath, LuaLocalPath));
            return Path.Combine(SourcePath, LuaLocalPath, tableName) + _Local_Table_Ext;
        }

        public static string GetLocalClientLuaPath(string excelPath)
        {
            string clientLuaPath = ExcelParserFileHelper.GetTempLuaPath(excelPath, false);
            clientLuaPath = clientLuaPath.Replace("/tmp/", LuaLocalPath.Substring(2));
            return clientLuaPath;
        }
    }
}
