using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ExcelTools.Scripts.Utils;
using ExcelTools.Scripts;
using ExcelTools.Scripts.UI;
using Lua;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Input;

namespace ExcelTools
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        //功能按钮状态
        private const string STATE_UPDATE = "Update";
        private const string STATE_REVERT = "Revert";
        private const string STATE_EDIT = "编辑";
        private const string STATE_FINISH_EDIT = "提交";

        private string _localRev;
        private string _serverRev;

        private ExcelFileListItem _excelItemChoosed = null;
        private IDListItem _IDItemSelected = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        List<Button> GenBtns;
        ObservableCollection<ExcelFileListItem> excelFiles = new ObservableCollection<ExcelFileListItem>();
        Dictionary<string, DifferController> _DiffDic = new Dictionary<string, DifferController>();
        const string _ConfigPath = "config.txt";
        List<string> _Folders = new List<string>(){
            "/serverexcel",
            "/SubConfigs"
        };
        const string ExcelPath = "../VersionToolRoot/Excel";
        const string _URL = "svn://svn.sg.xindong.com/RO/client-trunk";
        const string _FolderServerExcel = "/serverexcel";
        const string _FolderSubConfigs = "/SubConfigs";
        const string _Ext = ".xlsx";
        const string _TempRename = "_tmp";

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            excelListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            excelListView.Items.SortDescriptions.Add(new SortDescription("IsEditing", ListSortDirection.Descending));
            excelListView.Items.IsLiveSorting = true;
            propertyDataGrid.SelectionUnit = DataGridSelectionUnit.Cell;
            GetRevision();
            CheckStateBtn_Click(null, null);

            for(int i = 0; i < GlobalCfg.BranchCount; i++)
            {
                propertyDataGrid.Columns[i + 3].Header = Enum.GetName(typeof(Branch), i);
            }
            GenBtns = new List<Button>()
            {
                genTableBtn_Trunk,
                genTableBtn_Studio,
                genTableBtn_TF,
                genTableBtn_Release
            };
        }

        //1. 加载配置
        //1.1 设置源路径
        //1.2 加载所有文件
        #region 加载配置
        private void LoadConfig(bool force = false)
        {
            SetSourcePath(force);
            LoadFiles();
        }

        private void LoadFiles()
        {
            _Folders[0] = GlobalCfg.SourcePath + _FolderServerExcel;
            _Folders[1] = GlobalCfg.SourcePath + _FolderSubConfigs;
            excelFiles.Clear();
            List<string> files = FileUtil.CollectAllFolders(_Folders, _Ext);
            for (int i = 0; i < files.Count; i++)
            {
                excelFiles.Add(new ExcelFileListItem()
                {
                    Name = Path.GetFileNameWithoutExtension(files[i]),
                    IsSame = true,
                    IsEditing = false,
                    Paths = new List<string>(),
                    ClientServer = "C/S",
                    FilePath = files[i]
                });
            }
            excelListView.ItemsSource = excelFiles;
            //CheckStateBtn_Click(null, null);
        }

        private void SetSourcePath(bool force)
        {
            if (force || !File.Exists(_ConfigPath))
            {
                //使用相对路径固定表格位置
                GlobalCfg.SourcePath = ExcelPath;
            }
            using (StreamReader cfgSt = new StreamReader(_ConfigPath))
            {    
                GlobalCfg.SourcePath = cfgSt.ReadLine();
                cfgSt.Close();    
            }        
        }
        #endregion

        private void ChangeSourcePath_Click(object sender, RoutedEventArgs e)
        {
            LoadConfig(true);
        }

        private void FileListView_SelectionChange(object sender, SelectionChangedEventArgs e)
        {
            ListView listView = sender as ListView;
            if (listView != null)
            {
                ExcelFileListItem item = listView.SelectedItem as ExcelFileListItem;
                _excelItemChoosed = item;
            }
            _IDItemSelected = null;
            if (_excelItemChoosed == null)
            {
                idListView.ItemsSource = null;
                propertyDataGrid.ItemsSource = null;
                checkBox_changed.IsEnabled = false;
                checkBox_changed.IsChecked = false;
            }
            else
            {
                propertyDataGrid.ItemsSource = null;
                idListView.ItemsSource = GlobalCfg.Instance.GetIDList(_excelItemChoosed.FilePath);
                GlobalCfg.Instance.ResetPreviousIsNeedGen();
                checkBox_changed.IsEnabled = true;
                checkBox_changed.IsChecked = false;
            }
            JudgeMultiFuncBtnState();
            EditingModeRender();
            if (listView != null && idListView.ItemsSource == null)
            {
                listView.SelectedItem = null;
            }
        }

        private void IDListView_SelectChange(object sender, SelectionChangedEventArgs e)
        {
            IDListItem item = (sender as ListView).SelectedItem as IDListItem;
            if (item == null)
            {
                propertyDataGrid.ItemsSource = null;
                return;
            }
            _IDItemSelected = item;
            ObservableCollection<PropertyListItem> fieldList = new ObservableCollection<PropertyListItem>();

            Excel excel = GlobalCfg.Instance.GetParsedExcel(_excelItemChoosed.FilePath);
            if(excel == null)
            {
                (sender as ListView).SelectedItem = null;
                return;
            }
            List<PropertyInfo> propertyList = excel.Properties;
            List<lparser.config> configs = GlobalCfg.Instance.GetTableRows(item.ID);
            lparser.config fullConfig = configs[0];
            foreach(lparser.config config in configs)
            {
                if(config != null)
                {
                    if (config.properties.Count > fullConfig.properties.Count)
                    {
                        fullConfig = config;
                    }
                }
            }
            string ename = string.Empty;
            string cname = string.Empty;
            for (int i = 0, j = 0; i < fullConfig.properties.Count; i++)
            {
                ename = fullConfig.properties[i].name;
                while (propertyList[j].ename != ename)
                {
                    j++;
                }
                cname = propertyList[j].cname;
                fieldList.Add(new PropertyListItem()
                {
                    IsNeedGen = GlobalCfg.Instance.GetIsNeedGen(fullConfig.key, ename),
                    PropertyName = cname + "（" + ename + "）",
                    EnName = ename,
                    LocalContent = configs[0] != null && configs[0].propertiesDic.ContainsKey(ename) ? configs[0].propertiesDic[ename].value4Show : null,
                    Trunk = configs[1] != null && configs[1].propertiesDic.ContainsKey(ename) ? configs[1].propertiesDic[ename].value4Show : null,
                    Studio = configs[2] != null && configs[2].propertiesDic.ContainsKey(ename) ? configs[2].propertiesDic[ename].value4Show : null,
                    TF = configs[3] != null && configs[3].propertiesDic.ContainsKey(ename) ? configs[3].propertiesDic[ename].value4Show : null,
                    Release = configs[4] != null && configs[4].propertiesDic.ContainsKey(ename) ? configs[4].propertiesDic[ename].value4Show : null
                });
            }
            propertyDataGrid.ItemsSource = fieldList;
            RefreshGenBtnState();

            //刷新单元格颜色
            for (int j = 0; j < GlobalCfg.BranchCount; j++) {
                tablerowdiff trd = GlobalCfg.Instance.GetCellAllStatus(item.ID, j);
                if (trd == null) {
                    continue;
                }
                for (int a = 0; a < fieldList.Count; a++) {
                    if (trd.modifiedcells != null && trd.modifiedcells.ContainsKey(fieldList[a].EnName)){
                        DataGridCell dataGridCell = WPFHelper.GetCell(propertyDataGrid, a, j + 3);
                        dataGridCell.Background = Brushes.LightBlue;
                    }
                    if (trd.modifiedcells != null && trd.addedcells.ContainsKey(fieldList[a].EnName))
                    {
                        DataGridCell dataGridCell = WPFHelper.GetCell(propertyDataGrid, a, j + 3);
                        dataGridCell.Background = Brushes.LightPink;
                    }
                    if (trd.modifiedcells != null && trd.deletedcells.ContainsKey(fieldList[a].EnName))
                    {
                        DataGridCell dataGridCell = WPFHelper.GetCell(propertyDataGrid, a, j + 3);
                        dataGridCell.Background = Brushes.LightBlue;
                    }
                }
            }
        }

        private void CheckStateBtn_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, SVNHelper.FileStatusStr> statusDic = SVNHelper.AllStatus();
            for(int i = 0; i < excelFiles.Count; i++)
            {
                if (statusDic.ContainsKey(excelFiles[i].Name))
                {
                    excelFiles[i].IsSame = statusDic[excelFiles[i].Name].isSame;
                    excelFiles[i].Paths = statusDic[excelFiles[i].Name].paths;
                    //TODO:检测已经加锁的文件
                    //_ExcelFiles[i].IsEditing = statusDic[_ExcelFiles[i].Name].isLock;
                }
                else
                {
                    excelFiles[i].IsSame = true;
                    excelFiles[i].IsEditing = false;
                    excelFiles[i].Paths.Clear();
                }
            }
            JudgeMultiFuncBtnState();
            #region 插入deleted文件(已注释)
            //foreach (KeyValuePair<string, string[]> kv in statusDic)
            //{
            //    if (kv.Value[0] == SVNHelper.STATE_DELETED)
            //    {
            //        _ExcelFiles.Add(new ExcelFileListItem()
            //        {
            //            Name = Path.GetFileNameWithoutExtension(kv.Key),
            //            Status = kv.Value[0],
            //            LockByMe = kv.Value[1],
            //            ClientServer = "C/S",
            //            FilePath = kv.Key
            //        });
            //    }
            //}
            #endregion
        }

        private void MultiFuncBtn_Click(object sender, RoutedEventArgs e)
        {
            Button senderBtn = sender as Button;
            switch (senderBtn.Content)
            {
                case STATE_UPDATE:
                    SVNHelper.Update(_Folders[0], _Folders[1]);
                    GlobalCfg.Instance.ClearAll();
                    FileListView_SelectionChange(null, null);
                    GetRevision();
                    break;
                case STATE_REVERT:
                    SVNHelper.RevertAll(_excelItemChoosed.Paths);
                    CheckStateBtn_Click(null, null);
                    GlobalCfg.Instance.ClearCurrent();
                    FileListView_SelectionChange(null, null);
                    break;
                case STATE_EDIT:
                    //请求进入编辑状态
                    if (SVNHelper.RequestEdit(_excelItemChoosed.FilePath))
                    {
                        _excelItemChoosed.IsEditing = true;
                    }
                    else
                    {
                        _excelItemChoosed.IsEditing = false;
                    }
                    JudgeMultiFuncBtnState();
                    EditingModeRender();
                    break;
                case STATE_FINISH_EDIT:
                    System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.OKCancel;
                    System.Windows.Forms.DialogResult dr = System.Windows.Forms.MessageBox.Show("是否提交对于" + _excelItemChoosed.Name + "的修改？", "确认", buttons);
                    if (dr == System.Windows.Forms.DialogResult.OK)
                    {
                        for (int i = 0; i < GlobalCfg.BranchCount; i++)
                        {
                            GlobalCfg.Instance.ExcuteModified(i);
                        }
                        SVNHelper.ReleaseExcelRelative(_excelItemChoosed.FilePath);
                        _excelItemChoosed.IsEditing = false;
                        FileListView_SelectionChange(null, null);
                    }
                    break;
                default:
                    break;
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.OKCancel;
            System.Windows.Forms.DialogResult dr = System.Windows.Forms.MessageBox.Show("是否放弃所有修改？", "确认", buttons);
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                GlobalCfg.Instance.ClearCurrent();
                SVNHelper.ReleaseExcelRelative(_excelItemChoosed.FilePath);
                _excelItemChoosed.IsEditing = false;
                FileListView_SelectionChange(null, null);
            }
        }

        private void GetRevision()
        {
            _localRev = SVNHelper.GetLastestReversion(_Folders[0]);
            _serverRev = SVNHelper.GetLastestReversion(_URL);
            JudgeMultiFuncBtnState();
        }

        private void JudgeMultiFuncBtnState()
        {
            multiFunctionBtn.Visibility = Visibility.Visible;
            //需要Update
            if (_localRev != _serverRev)
            {
                multiFunctionBtn.Content = STATE_UPDATE;
            }
            //和SVN版本库中有差异(MODIFIED和ADDED)
            else if (_excelItemChoosed != null && !_excelItemChoosed.IsSame)
            {
                multiFunctionBtn.Content = STATE_REVERT;
            }
            //可请求进入编辑状态
            else if (_excelItemChoosed != null && _excelItemChoosed.IsSame && !_excelItemChoosed.IsEditing)
            {
                multiFunctionBtn.Content = STATE_EDIT;
            }
            else if (_excelItemChoosed != null && _excelItemChoosed.IsEditing)
            {
                multiFunctionBtn.Content = STATE_FINISH_EDIT;
                
            }
            else
            {
                multiFunctionBtn.Visibility = Visibility.Hidden;
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            //do my stuff before closing
            FileUtil.DeleteHiddenFile(new List<string> { GlobalCfg.SourcePath + "/.." }, _Ext);
            FileUtil.DeleteHiddenFile(new List<string> { GlobalCfg.SourcePath + "/.." }, ".txt");
            SVNHelper.ReleaseAll();
            base.OnClosing(e);
        }

        static List<string> branchs = new List<string>{ "genTableBtn_Trunk", "genTableBtn_Studio", "genTableBtn_TF", "genTableBtn_Release" };
        private const string GENBTN_STATE_GEN = "生成至";
        private const string GENBTN_STATE_CANCEL = "取消生成";

        private void GenTableBtn_Click(object sender, RoutedEventArgs e)
        {
            Button genBtn = sender as Button;
            string aimUrl = string.Empty;
            string tmpPath = string.Empty;
            int idx = branchs.IndexOf(genBtn.Name);
            if (idx > -1 && _IDItemSelected != null)
            {
                switch (genBtn.DataContext) {
                    case GENBTN_STATE_GEN:
                        GlobalCfg.Instance.ApplyRow(idx, _IDItemSelected);
                        _IDItemSelected.ReverseIsApply(idx);
                        break;
                    case GENBTN_STATE_CANCEL:
                        GlobalCfg.Instance.CancelRow(idx, _IDItemSelected);
                        _IDItemSelected.ReverseIsApply(idx);
                        break;
                    default:
                        break;
                }
            }
            //刷新修改
            IDListView_SelectChange(idListView, null);
        }

        private void PropertyCheckBox_Click(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            DataGridRow parentRow = WPFHelper.GetParentObject<DataGridRow>(checkBox, null);
            PropertyListItem sourceItem = parentRow.Item as PropertyListItem;
            GlobalCfg.Instance.SetCurrentIsNeedGen(_IDItemSelected.ID, sourceItem.EnName, checkBox.IsChecked.Value);
        }

        private void DataGridCell_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!_excelItemChoosed.IsEditing)
                return;
            DataGridCell cell = sender as DataGridCell;
            DataGridRow row = WPFHelper.GetParentObject<DataGridRow>(cell, null);
            PropertyListItem itemSource = row.Item as PropertyListItem;
            string branchName;
            for (int i = 0; i < GlobalCfg.BranchCount; i++)
            {
                branchName = Enum.GetName(typeof(Branch), i);
                if ((string)(cell.Column.Header) == branchName)
                {
                    PropertyEditWindow propertyEditWindow = new PropertyEditWindow(itemSource, _IDItemSelected.ID, i);
                    propertyEditWindow.ShowDialog();
                    break;
                }
            }
        }

        private void EditingModeRender()
        {
            RefreshCancelBtn();
            RefreshCheckBoxColumn();
            RefreshGenBtnState();
        }

        private void RefreshGenBtnState()
        {
            List<string> rowStatus = _IDItemSelected == null ? null : GlobalCfg.Instance.GetRowAllStatus(_IDItemSelected.ID);
            for (int i = 0; i < GenBtns.Count; i++)
            {
                if (_IDItemSelected != null && _IDItemSelected.IsApplys[i])
                {
                    GenBtns[i].Content = GENBTN_STATE_CANCEL + branchs[i].Substring(branchs[0].IndexOf("_")+1);
                    GenBtns[i].DataContext = GENBTN_STATE_CANCEL;
                }
                else
                {
                    GenBtns[i].Content = GENBTN_STATE_GEN + branchs[i].Substring(branchs[0].IndexOf("_") + 1);
                    GenBtns[i].DataContext = GENBTN_STATE_GEN;
                }
                
                GenBtns[i].IsEnabled = _excelItemChoosed == null? false: _excelItemChoosed.IsEditing;
                if (rowStatus == null || (rowStatus[i] == "" && !_IDItemSelected.IsApplys[i]))
                {
                    GenBtns[i].IsEnabled = false;
                }
            }
        }

        private void RefreshCancelBtn()
        {
            if (_excelItemChoosed != null && _excelItemChoosed.IsEditing)
            {
                cancelBtn.Visibility = Visibility.Visible;
            }
            else
            {
                cancelBtn.Visibility = Visibility.Hidden;
            }
        }

        private void RefreshCheckBoxColumn()
        {
            if (_excelItemChoosed == null || _excelItemChoosed.IsEditing)
            {
                checkBoxColumn.Visibility = Visibility.Visible;
            }
            else
            {
                checkBoxColumn.Visibility = Visibility.Hidden;
            }
        }

        #region 过滤操作（搜索，筛选）
        private bool IsSearchingExcel = false;
        private bool IsSearchingId = false;
        private bool IsCheckEditing = false;
        private bool IsCheckChanged = false;
        private bool IsCheckApplyed = false;
        private string excelSearchKey = null;
        private string idSearchKey = null;

        private void SearchBox_OnSearch(object sender, SearchEventArgs e)
        {
            SearchBox searchBox = sender as SearchBox;
            switch (searchBox.Name)
            {
                case "excelSearchBox":
                    IsSearchingExcel = true;
                    excelSearchKey = e.SearchText;
                    FilterItems(excelListView);
                    break;
                case "idSearchBox":
                    IsSearchingId = true;
                    idSearchKey = e.SearchText;
                    FilterItems(idListView);
                    break;
                default:
                    break;
            }
        }

        private void SearchBox_OnCancelSearch(object sender, CancelSearchEventArgs e)
        {
            SearchBox searchBox = sender as SearchBox;
            switch (searchBox.Name)
            {
                case "excelSearchBox":
                    IsSearchingExcel = false;
                    excelSearchKey = null;
                    FilterItems(excelListView);
                    break;
                case "idSearchBox":
                    IsSearchingId = false;
                    idSearchKey = null;
                    if (_excelItemChoosed != null)
                        FilterItems(idListView);
                    break;
                default:
                    break;
            }
        }

        const string CHECKBOX_EDITING_NAME = "checkBox_editing";
        const string CHECKBOX_CHANGED_NAME = "checkBox_changed";
        const string FILTER_BY_EDITING = "IsEditing";
        const string FILTER_BY_STATES = "States";
        const string FILTER_BY_APPLY = "IsApplys";

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            switch (checkBox.Name)
            {
                case CHECKBOX_EDITING_NAME:
                    IsCheckEditing = true;
                    FilterItems(excelListView);
                    JudgeMultiFuncBtnState();
                    break;
                case CHECKBOX_CHANGED_NAME:
                    IsCheckChanged = true;
                    FilterItems(idListView);
                    break;
                default:
                    break;
            }
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            switch (checkBox.Name)
            {
                case CHECKBOX_EDITING_NAME:
                    IsCheckEditing = false;
                    FilterItems(excelListView);
                    JudgeMultiFuncBtnState();
                    break;
                case CHECKBOX_CHANGED_NAME:
                    IsCheckChanged = false;
                    FilterItems(idListView);
                    break;
                default:
                    break;
            }
        }

        private void FilterItems(ListView list)
        {
            switch (list.Name)
            {
                case "excelListView":
                    ObservableCollection<ExcelFileListItem> excelRes = excelFiles;
                    if (IsCheckEditing) {
                        excelRes = GetFilteredCollection(excelRes, FILTER_BY_EDITING);
                    }
                    list.ItemsSource = excelRes;
                    if (IsSearchingExcel)
                        excelRes = GetMatchItem(excelRes, excelSearchKey);
                    list.ItemsSource = excelRes;
                    break;
                case "idListView":
                    if(_excelItemChoosed == null)
                    {
                        return;
                    }
                    ObservableCollection<IDListItem> idRes = GlobalCfg.Instance.GetIDList(_excelItemChoosed.FilePath);
                    if (IsCheckChanged)
                    {
                        idRes = GetFilteredCollection(idRes, FILTER_BY_STATES);
                    }
                    list.ItemsSource = idRes;
                    if (IsSearchingId)
                        idRes = GetMatchItem(idRes, idSearchKey);
                    list.ItemsSource = idRes;
                    break;
            }
            #region 获得搜索数据
            ObservableCollection<T> GetMatchItem<T>(ObservableCollection<T> sourceItem, string input)
            {
                bool IsMatchFromStart = true;
                if (input.StartsWith("*"))
                {
                    IsMatchFromStart = false;
                    input = input.Substring(1);
                }
                ObservableCollection<T> searchRes = new ObservableCollection<T>();
                for (int i = 0; i < sourceItem.Count; i++)
                {
                    T item = (T)sourceItem[i];
                    string ss = null;
                    if (item is ExcelFileListItem)
                    {
                        ExcelFileListItem excelItem = item as ExcelFileListItem;
                        ss = excelItem.Name;
                    }
                    else if (item is IDListItem)
                    {
                        IDListItem idItem = item as IDListItem;
                        ss = idItem.IdDisplay;
                    }
                    else
                    {
                        return null;
                    }
                    if (StringHelper.StringMatch(ss, input, IsMatchFromStart, false))
                    {
                        searchRes.Add(item);
                    }
                }
                return searchRes;
            }
            #endregion
            #region 获得过滤后数据
            ObservableCollection<T> GetFilteredCollection<T>(ObservableCollection<T> sourceItem, string filterBy)
            {
                ObservableCollection<T> filteredCollection = new ObservableCollection<T>();
                for (int j = 0; j < sourceItem.Count; j++)
                {
                    T item = (T)sourceItem[j];
                    if (item is ExcelFileListItem && filterBy == FILTER_BY_EDITING)
                    {
                        ExcelFileListItem excelItem = item as ExcelFileListItem;
                        if (excelItem.IsEditing)
                        {
                            filteredCollection.Add(item);
                        }
                    }
                    else if (item is IDListItem)
                    {
                        IDListItem idItem = item as IDListItem;
                        if (filterBy == FILTER_BY_STATES)
                        {
                            for (int i = 0; i < idItem.States.Count; i++)
                            {
                                if (idItem.States[i] != "")
                                {
                                    filteredCollection.Add(item);
                                    break;
                                }
                                else if (idItem.IsApplys[i])
                                {
                                    filteredCollection.Add(item);
                                    break;
                                }
                            }
                        }
                    }
                }
                return filteredCollection;
            }
            #endregion
        }

        #endregion
    }
}