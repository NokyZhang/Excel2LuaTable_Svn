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
        //生成按钮状态
        private const string STATE_GEN = "生成至";
        private const string STATE_CANCEL = "取消生成";

        private string _localRev;
        private string _serverRev;

        private ExcelFileListItem _excelItemChoosed = null;
        private IDListItem _IDItemSelected = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        List<Button> GenBtns;
        ObservableCollection<ExcelFileListItem> _ExcelFiles = new ObservableCollection<ExcelFileListItem>();
        Dictionary<string, DifferController> _DiffDic = new Dictionary<string, DifferController>();
        const string _ConfigPath = "config.txt";
        List<string> _Folders = new List<string>(){
            "/serverexcel",
            "/SubConfigs"
        };
        const string _URL = "svn://svn.sg.xindong.com/RO/client-trunk";
        const string _FolderServerExvel = "/serverexcel";
        const string _FolderSubConfigs = "/SubConfigs";
        const string _Ext = ".xlsx";
        const string _TempRename = "_tmp";

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            #region 初始化各列表
            excelListView.SelectionChanged += FileListView_SelectionChange;
            idListView.SelectionChanged += IDListView_SelectChange;
            excelListView.Items.SortDescriptions.Add(new SortDescription("Name", ListSortDirection.Ascending));
            excelListView.Items.SortDescriptions.Add(new SortDescription("IsEditing", ListSortDirection.Descending));
            excelListView.Items.IsLiveSorting = true;
            propertyDataGrid.SelectionUnit = DataGridSelectionUnit.Cell;
            #endregion
            GetRevision();
            CheckStateBtn_Click(null, null);

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
            _Folders[0] = GlobalCfg.SourcePath + _FolderServerExvel;
            _Folders[1] = GlobalCfg.SourcePath + _FolderSubConfigs;
            _ExcelFiles.Clear();
            List<string> files = FileUtil.CollectAllFolders(_Folders, _Ext);
            for (int i = 0; i < files.Count; i++)
            {
                _ExcelFiles.Add(new ExcelFileListItem()
                {
                    Name = Path.GetFileNameWithoutExtension(files[i]),
                    IsSame = true,
                    IsEditing = false,
                    Paths = new List<string>(),
                    ClientServer = "C/S",
                    FilePath = files[i]
                });
            }
            excelListView.ItemsSource = _ExcelFiles;
            //CheckStateBtn_Click(null, null);
        }

        private void SetSourcePath(bool force)
        {
            if (force || !File.Exists(_ConfigPath))
            {
                ChooseSourcePath();
            }
            using (StreamReader cfgSt = new StreamReader(_ConfigPath))
            {    
                GlobalCfg.SourcePath = cfgSt.ReadLine();
                cfgSt.Close();    
            }        
        }

        private void ChooseSourcePath()
        {
            System.Windows.Forms.FolderBrowserDialog folderBrowser = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择Cehua/Table位置：",
                ShowNewFolderButton = false,
            };
            folderBrowser.ShowDialog();
            string path = folderBrowser.SelectedPath.Replace(@"\", "/");
            using (StreamWriter cfgSt = new StreamWriter(_ConfigPath))
            {
                cfgSt.WriteLine(path);
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
            if (sender != null)
            {
                ListView listView = sender as ListView;
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
                checkBox_applyed.IsEnabled = false;
                checkBox_applyed.IsChecked = false;
            }
            else
            {
                JudgeMultiFuncBtnState();
                propertyDataGrid.ItemsSource = null;
                idListView.ItemsSource = GlobalCfg.Instance.GetIDList(_excelItemChoosed.FilePath);
                checkBox_changed.IsEnabled = true;
                checkBox_changed.IsChecked = false;
                checkBox_applyed.IsEnabled = true;
                checkBox_applyed.IsChecked = false;
                ResetGenBtnState();
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
            List<PropertyInfo> propertyList = excel.Properties;
            List<lparser.config> configs = GlobalCfg.Instance.GetTableRow(item.ID);
            string ename = string.Empty;
            for (int i = 0; i < propertyList.Count; i++)
            {
                ename = propertyList[i].ename;
                fieldList.Add(new PropertyListItem()
                {
                    PropertyName = propertyList[i].cname + "（" + ename + "）",
                    EnName = ename,
                    Context = configs[0] != null && configs[0].propertiesDic.ContainsKey(ename) ? configs[0].propertiesDic[ename].value : null,
                    Trunk = configs[1] != null && configs[1].propertiesDic.ContainsKey(ename) ? configs[1].propertiesDic[ename].value : null,
                    Studio = configs[2] != null && configs[2].propertiesDic.ContainsKey(ename) ? configs[2].propertiesDic[ename].value : null,
                    TF = configs[3] != null && configs[3].propertiesDic.ContainsKey(ename) ? configs[3].propertiesDic[ename].value : null,
                    Release = configs[4] != null && configs[4].propertiesDic.ContainsKey(ename) ? configs[4].propertiesDic[ename].value : null
                });
            }
            propertyDataGrid.ItemsSource = fieldList;
            ResetGenBtnState();

            //刷新单元格颜色
            for (int j = 0; j < GlobalCfg.BranchCount; j++) {
                tablerowdiff trd = GlobalCfg.Instance.GetCellAllStatus(item.ID, j);
                if (trd == null) {
                    continue;
                }
                for (int a = 0; a < fieldList.Count; a++) {
                    if (trd.modifiedcells != null && trd.modifiedcells.ContainsKey(fieldList[a].EnName)){
                        DataGridCell dataGridCell = WPFHelper.GetCell(propertyDataGrid, a, j + 2);
                        dataGridCell.Background = Brushes.LightBlue;
                    }
                    if (trd.modifiedcells != null && trd.addedcells.ContainsKey(fieldList[a].EnName))
                    {
                        DataGridCell dataGridCell = WPFHelper.GetCell(propertyDataGrid, a, j + 2);
                        dataGridCell.Background = Brushes.LightPink;
                    }
                    if (trd.modifiedcells != null && trd.deletedcells.ContainsKey(fieldList[a].EnName))
                    {
                        DataGridCell dataGridCell = WPFHelper.GetCell(propertyDataGrid, a, j + 2);
                        dataGridCell.Background = Brushes.LightBlue;
                    }
                }
            }
        }

        private void CheckStateBtn_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, SVNHelper.FileStatusStr> statusDic = SVNHelper.AllStatus();
            for(int i = 0; i < _ExcelFiles.Count; i++)
            {
                if (statusDic.ContainsKey(_ExcelFiles[i].Name))
                {
                    _ExcelFiles[i].IsSame = statusDic[_ExcelFiles[i].Name].isSame;
                    _ExcelFiles[i].Paths = statusDic[_ExcelFiles[i].Name].paths;
                    _ExcelFiles[i].IsEditing = statusDic[_ExcelFiles[i].Name].isLock;
                }
                else
                {
                    _ExcelFiles[i].IsSame = true;
                    _ExcelFiles[i].IsEditing = false;
                    _ExcelFiles[i].Paths.Clear();
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
                    if(_IDItemSelected != null)
                    {
                        ResetGenBtnState();
                    }
                    break;
                case STATE_FINISH_EDIT:
                    System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.OKCancel;
                    System.Windows.Forms.DialogResult dr = System.Windows.Forms.MessageBox.Show("是否放弃提交修改？", "确认", buttons);
                    if (dr == System.Windows.Forms.DialogResult.OK)
                    {
                        for (int i = 0; i < GlobalCfg.BranchCount; i++)
                        {
                            GlobalCfg.Instance.ExcuteModified(i);
                        }
                        SVNHelper.ReleaseExcelRelative(_excelItemChoosed.FilePath);
                        _excelItemChoosed.IsEditing = false;
                        JudgeMultiFuncBtnState();
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
                FileListView_SelectionChange(null, null);
                SVNHelper.ReleaseExcelRelative(_excelItemChoosed.FilePath);
                _excelItemChoosed.IsEditing = false;
                JudgeMultiFuncBtnState();
            }
        }

        private void ResetGenBtnState()
        {
            List<string> rowStatus = _IDItemSelected == null? null:GlobalCfg.Instance.GetRowAllStatus(_IDItemSelected.ID);
            for (int i = 0; i < GenBtns.Count; i++)
            {
                if (_IDItemSelected != null && _IDItemSelected.IsApplys[i])
                {
                    GenBtns[i].Content = STATE_CANCEL;
                }
                else
                {
                    GenBtns[i].Content = STATE_GEN;
                }

                GenBtns[i].IsEnabled = _excelItemChoosed.IsEditing;
                if (rowStatus == null || (rowStatus[i] == "" && (GenBtns[i].Content.ToString() == STATE_GEN)))
                {
                    GenBtns[i].IsEnabled = false;
                }
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

            RefreshCancelBtn();
        }

        private void RefreshCancelBtn()
        {
            if(_excelItemChoosed != null && _excelItemChoosed.IsEditing)
            {
                cancelBtn.Visibility = Visibility.Visible;
            }
            else
            {
                cancelBtn.Visibility = Visibility.Hidden;
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
        private void GenTableBtn_Click(object sender, RoutedEventArgs e)
        {
            Button genBtn = sender as Button;
            string aimUrl = string.Empty;
            string tmpPath = string.Empty;
            int idx = branchs.IndexOf(genBtn.Name);
            if (idx > -1 && _IDItemSelected != null)
            {
                switch (genBtn.Content) {
                    case STATE_GEN:
                        GlobalCfg.Instance.ApplyRow(idx, _IDItemSelected);
                        _IDItemSelected.IsApplys[idx] = true;
                        break;
                    case STATE_CANCEL:
                        GlobalCfg.Instance.CancelRow(idx, _IDItemSelected);
                        _IDItemSelected.IsApplys[idx] = false;
                        break;
                }
            }
            //刷新修改
            IDListView_SelectChange(idListView, null);
        }

        private void SearchBox_OnSearch(object sender, SearchEventArgs e)
        {
            SearchBox searchBox = sender as SearchBox;
            switch (searchBox.Name)
            {
                case "excelSearchBox":
                    ScrollToMatchItem<ExcelFileListItem>(excelListView, e.SearchText);
                    break;
                case "idSearchBox":
                    ScrollToMatchItem<IDListItem>(idListView, e.SearchText);
                    break;
                default:
                    break;
            }

            void ScrollToMatchItem<T>(ListView listView, string input)
            {
                bool IsMatchFromStart = true;
                if (input.StartsWith("*"))
                {
                    IsMatchFromStart = false;
                    input = input.Substring(1);
                }
                for(int i = 0; i < listView.Items.Count; i++)
                {
                    T item = (T)listView.Items[i];
                    string ss = null;
                    if(item is ExcelFileListItem)
                    {
                        ExcelFileListItem excelItem = item as ExcelFileListItem;
                        ss = excelItem.Name;
                    }
                    else if(item is IDListItem)
                    {
                        IDListItem idItem = item as IDListItem;
                        ss = idItem.IdDisplay;
                    }
                    else
                    {
                        return;
                    }
                    if(StringHelper.StringMatch(ss, input, IsMatchFromStart, false))
                    {
                        listView.ScrollIntoView(item);
                        listView.SelectedItem = item;
                        return;
                    }
                }
                return;
            }
        }

        const string CHECKBOX_EDITING_NAME = "checkBox_editing";
        const string CHECKBOX_CHANGED_NAME = "checkBox_changed";
        const string CHECKBOX_APPLYED_NAME = "checkBox_applyed";
        const string FILTER_BY_EDITING = "IsEditing";
        const string FILTER_BY_STATES = "States";
        const string FILTER_BY_APPLY = "IsApplys";

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            switch (checkBox.Name)
            {
                case CHECKBOX_EDITING_NAME:
                    ObservableCollection<ExcelFileListItem> editFilteredCollection =
                        GetFilteredCollection(_ExcelFiles, FILTER_BY_EDITING);
                    excelListView.ItemsSource = editFilteredCollection;
                    break;
                case CHECKBOX_CHANGED_NAME:
                    checkBox_applyed.IsChecked = false;
                    ObservableCollection<IDListItem> changedFilteredCollection =
                        GetFilteredCollection(GlobalCfg.Instance.GetIDList(_excelItemChoosed.FilePath), FILTER_BY_STATES);
                    idListView.ItemsSource = changedFilteredCollection;
                    break;
                case CHECKBOX_APPLYED_NAME:
                    checkBox_changed.IsChecked = false;
                    ObservableCollection<IDListItem> applyFilteredCollection =
                        GetFilteredCollection(GlobalCfg.Instance.GetIDList(_excelItemChoosed.FilePath), FILTER_BY_APPLY);
                    idListView.ItemsSource = applyFilteredCollection;
                    break;
                default:
                    break;
            }
            #region 获得过滤后数据
            ObservableCollection<T> GetFilteredCollection<T>(ObservableCollection<T> sourceCollection, string filterBy)
            {
                ObservableCollection<T> filteredCollection = new ObservableCollection<T>();
                foreach(T item in sourceCollection)
                {
                    if(item is ExcelFileListItem && filterBy == FILTER_BY_EDITING)
                    {
                        ExcelFileListItem excelItem = item as ExcelFileListItem;
                        if (excelItem.IsEditing)
                        {
                            filteredCollection.Add(item);
                        }
                    }
                    else if(item is IDListItem)
                    {
                        IDListItem idItem = item as IDListItem;
                        if(filterBy == FILTER_BY_STATES)
                        {
                            for(int i = 0; i < idItem.States.Count; i++)
                            {
                                if(idItem.States[i] != "")
                                {
                                    filteredCollection.Add(item);
                                    break;
                                }
                            }
                        }
                        else if(filterBy == FILTER_BY_APPLY)
                        {
                            for (int i = 0; i < idItem.IsApplys.Count; i++)
                            {
                                if (idItem.IsApplys[i])
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

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            CheckBox checkBox = sender as CheckBox;
            switch (checkBox.Name)
            {
                case CHECKBOX_EDITING_NAME:
                    excelListView.ItemsSource = _ExcelFiles;
                    break;
                case CHECKBOX_CHANGED_NAME:
                case CHECKBOX_APPLYED_NAME:
                    idListView.ItemsSource = GlobalCfg.Instance.GetIDList(_excelItemChoosed.FilePath);
                    break;
                default:
                    break;
            }
        }
    }
}