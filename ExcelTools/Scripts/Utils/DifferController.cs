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
        //本地修改的文件路径
        private string _localPath;
        public string LocalPath
        {
            get { return _localPath; }
        }
        //
        private string _tempPath;
        public string TempPath
        {
            get { return _tempPath; }
        }

        public const string STATUS_NONE = "";
        public const string STATUS_ADDED = "A";
        public const string STATUS_DELETED = "D";
        public const string STATUS_MODIFIED = "M";


        //存储TempPath文件中被删除的行号
        private List<int> _deletedList = new List<int>();
        //存储localExcelPath文件中被添加的行号
        private List<int> _addedList = new List<int>();
        //存储TempPath文件中需要插入的行号
        private List<int> _addedToList = new List<int>();
        //存储deletedList和addedList中的公共值
        private List<int> _modifiedList = new List<int>();


        //不执行的修改
        private List<int> _cancelList = new List<int>();
        private bool _isCancelChanges = false;

        public void RevertModified(int row)
        {
            if (!_cancelList.Contains(row))
            {
                _cancelList.Add(row);
            }
            CancelChanges(_cancelList);
            RefreshUIData();
        }

        #region 以下为UI绑定数据及设置
        private ObservableCollection<IDListItem> _idListItems;
        public ObservableCollection<IDListItem> IDListItems
        {
            get
            {
                if(_idListItems == null)
                {
                    _idListItems = new ObservableCollection<IDListItem>();
                }
                return _idListItems;
            }
        }
        #endregion

        public DifferController(string localExcelPath, string tempPath)
        {
            _localPath = localExcelPath;
            _tempPath = tempPath;
        }

        public bool Differ()
        {
            Excel localExcel = GlobalCfg.Instance.GetParsedExcel(_localPath);
            Excel tempExcel = GlobalCfg.Instance.GetParsedExcel(_tempPath);
            string localExcelTmp = localExcel.ToString();
            string tempTmp = tempExcel.ToString();
            if (localExcelTmp == tempTmp)
            {
                return false;
            }
            else
            {
                _deletedList.Clear();
                _addedList.Clear();
                _addedToList.Clear();
                _modifiedList.Clear();
                for (int i = 0; i < tempExcel.rows.Count; i++)
                {
                    string rowStr = tempExcel.rows[i].ToString();
                    if (rowStr != null &&!localExcelTmp.Contains(rowStr))
                    {
                        _deletedList.Add(i + 5);
                    }
                }
                for (int i = 0; i < localExcel.rows.Count; i++)
                {
                    string rowStr = localExcel.rows[i].ToString();
                    if (rowStr != null &&!tempTmp.Contains(rowStr))
                    {
                        _addedList.Add(i + 5);
                        _addedToList.Add(i + 5);
                    }
                }
                IEnumerable<int> en = _deletedList.Intersect(_addedList);
                foreach (int index in en)
                {
                    _modifiedList.Add(index);
                }
                RefreshUIData();
                return true;
            }
        }

        //刷新UI绑定的数据
        private void RefreshUIData()
        {
            IDListItems.Clear();
            for (int i = 0; i < _modifiedList.Count; i++)
            {
                IDListItems.Add(new IDListItem()
                {
                    ID = GlobalCfg.Instance.GetParsedExcel(_localPath).rows[_modifiedList[i] - 5].cells[0].GetValue(),
                    Row = _modifiedList[i],
                });
            }
            for (int i = 0; i < _addedList.Count; i++)
            {
                if (!_modifiedList.Contains(_addedList[i]))
                {
                    IDListItems.Add(new IDListItem()
                    {
                        ID = GlobalCfg.Instance.GetParsedExcel(_localPath).rows[_addedList[i] - 5].cells[0].GetValue(),
                        Row = _addedList[i],
                    });
                }
            }
            for (int i = 0; i < _deletedList.Count; i++)
            {
                if (!_modifiedList.Contains(_deletedList[i]))
                {
                    IDListItems.Add(new IDListItem()
                    {
                        ID = GlobalCfg.Instance.GetParsedExcel(_tempPath).rows[_deletedList[i] - 5].cells[0].GetValue(),
                        Row = _deletedList[i],
                    });
                }
            }
        }

        public void ConfirmChangesAndCommit(string branch, string aimUrl)
        {
            CancelChanges(_cancelList);
            //若不整张表提交，就去修改临时文件，用以提交
            if (_isCancelChanges)
            {
                ModifyTempFile();
                string ogName = Path.GetFileName(LocalPath);
                FileUtil.RenameFile(LocalPath, ogName.Insert(ogName.IndexOf("."), "_local"));
                FileUtil.RenameFile(TempPath, ogName);
            }
            string tmpTablePath = branch  + "Table_" + Path.GetFileNameWithoutExtension(LocalPath) + ".txt";
            ExcelParser.ParseTemp(LocalPath, tmpTablePath);
            loptimal.optimal(tmpTablePath, tmpTablePath);            
            //TODO:提交
            //表格提交
            //SVNHelper.Commit(LocalPath);
            //配置提交
            //
        }


        //取消个别改动时用这个，全部取消直接Revert
        private void CancelChanges(List<int> rowsExclusion)
        {
            for (int i = 0; i < rowsExclusion.Count; i++)
            {
                if (_modifiedList.IndexOf(rowsExclusion[i]) != -1)
                {
                    _modifiedList.Remove(rowsExclusion[i]);
                    _deletedList.Remove(rowsExclusion[i]);
                    int index = _addedList.IndexOf(rowsExclusion[i]);
                    _addedList.RemoveAt(index);
                    _addedToList.RemoveAt(index);
                    _isCancelChanges = true;
                }
                else if (_deletedList.IndexOf(rowsExclusion[i]) != -1)
                {
                    for (int j = 0; j < _addedList.Count; j++)
                    {
                        if (_addedList[j] > _deletedList[i])
                        {
                            _addedToList[j]++;
                        }
                    }
                    _deletedList.RemoveAt(_deletedList.IndexOf(rowsExclusion[i]));
                    _isCancelChanges = true;
                }
                else if (_addedList.IndexOf(rowsExclusion[i]) != -1)
                {
                    int index = _addedList.IndexOf(rowsExclusion[i]);
                    for (int j = _addedList.IndexOf(rowsExclusion[i]) + 1; j < _addedList.Count; j++)
                    {
                        _addedToList[j]--;
                    }
                    _addedList.RemoveAt(index);
                    _addedToList.RemoveAt(index);
                    _isCancelChanges = true; ;
                }
            }
        }

        private void ModifyTempFile()
        {
            XSSFWorkbook tmpWk = null;
            XSSFWorkbook locWk = null;
            using (FileStream tmpFs = File.Open(_tempPath, FileMode.Open, FileAccess.ReadWrite))
            {
                tmpWk = new XSSFWorkbook(tmpFs);
                tmpFs.Close();
            }
            using (FileStream locFs = File.Open(_localPath, FileMode.Open, FileAccess.Read))
            {
                locWk = new XSSFWorkbook(locFs);
                locFs.Close();
            }
            ISheet tmpSheet = tmpWk.GetSheetAt(0);
            ISheet locSheet = locWk.GetSheetAt(0);

            //修改行
            //for (int i = 0; i < modifiedList.Count; i++)
            //{
            //    IRow tmpRow = tmpSheet.GetRow(modifiedList[i] - 1);
            //    IRow locRow = locSheet.GetRow(modifiedList[i] - 1);
            //    for (int j = 0; j < tmpRow.Cells.Count; j++)
            //    {
            //        ICell cell = tmpRow.GetCell(j);
            //        //cell.SetCellValue("");
            //        tmpRow.RemoveCell(cell);
            //    }
            //    for (int j = 0; j < locRow.Cells.Count; j++)
            //    {
            //        ICell cell = tmpRow.CreateCell(j);
            //        //ICell cell = tmpRow.GetCell(j);
            //        if (locRow.GetCell(j).CellType == CellType.Numeric)
            //            cell.SetCellValue(locRow.GetCell(j).NumericCellValue);
            //        else if (locRow.GetCell(j).CellType == CellType.String)
            //            cell.SetCellValue(locRow.GetCell(j).StringCellValue);
            //    }
            //}

            //删除行
            for (int i = 0; i < _deletedList.Count; i++)
            {
                IRow tmpRow = tmpSheet.GetRow(_deletedList[i] - 1);
                tmpSheet.RemoveRow(tmpRow);
            }
            //紧凑
            for (int i = 0; i <= tmpSheet.LastRowNum; i++)
            {
                if (tmpSheet.GetRow(i) == null)
                {
                    tmpSheet.ShiftRows(i + 1, tmpSheet.LastRowNum, -1, true, true);
                    i--;
                }
            }
            //插入行
            for (int i = 0; i < _addedList.Count; i++)
            {
                if (_addedList[i] - 1 <= tmpSheet.LastRowNum)
                {
                    tmpSheet.ShiftRows(_addedToList[i] - 1, tmpSheet.LastRowNum, 1, true, true);
                }
                IRow tmpRow = tmpSheet.CreateRow(_addedToList[i] - 1);
                IRow locRow = locSheet.GetRow(_addedList[i] - 1);
                for (int j = 0; j < locRow.LastCellNum; j++)
                {
                    ICell tmpCell = tmpRow.CreateCell(j);
                    ICell locCell = locRow.GetCell(j);
                    if (locCell != null)
                    {
                        ICellStyle cellStyle = tmpWk.CreateCellStyle();
                        cellStyle.CloneStyleFrom(locCell.CellStyle);
                        tmpCell.CellStyle = cellStyle;
                        if (locCell.CellType == CellType.Numeric)
                            tmpCell.SetCellValue(locCell.NumericCellValue);
                        else if (locCell.CellType == CellType.String)
                            tmpCell.SetCellValue(locCell.StringCellValue);
                        else if (locCell.CellType == CellType.Blank)
                            tmpCell.SetCellValue(locCell.StringCellValue);
                    }
                }
            }

            FileUtil.SetHidden(_tempPath, false);
            using (FileStream tmpFs = File.Create(_tempPath))
            {
                tmpWk.Write(tmpFs);
                tmpFs.Close();
            }
            FileUtil.SetHidden(_tempPath, true);
        }

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
