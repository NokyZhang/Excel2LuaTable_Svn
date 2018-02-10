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
using static SVNHelper;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

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

        private ExcelFileListItem _fileItemChoosed = null;
        private IDListItem _IDItemSelected = null;

        public MainWindow()
        {
            InitializeComponent();
        }

        List<Button> GenBtns;
        CollectionViewSource view = new CollectionViewSource();
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
            tableListView.DataContext = view;
            tableListView.SelectionChanged += FileListView_SelectionChange;
            idListView.SelectionChanged += IDListView_SelectChange;
            //tableListView.Items.SortDescriptions.Add(new SortDescription("IsEditing", ListSortDirection.Descending));
            tableListView.Items.IsLiveSorting = true;
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
            view.Source = _ExcelFiles;
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
                _fileItemChoosed = item;
            }
            if (_fileItemChoosed == null)
            {
                return;
            }
            _IDItemSelected = null;
            JudgeMultiFuncBtnState();
            idListView.ItemsSource = null;
            propertyDataGrid.ItemsSource = null;
            ResetGenBtnEnable();
            idListView.ItemsSource = GlobalCfg.Instance.GetIDList(_fileItemChoosed.FilePath);
            ResetGenBtnState();
        }

        private void IDListView_SelectChange(object sender, SelectionChangedEventArgs e)
        {
            IDListItem item = (sender as ListView).SelectedItem as IDListItem;
            if(item == null)
                return;

            Excel excel = GlobalCfg.Instance.GetParsedExcel(_fileItemChoosed.FilePath);
            List<PropertyInfo> propertyList = excel.Properties;
            ObservableCollection<PropertyListItem> fieldList = new ObservableCollection<PropertyListItem>();

            _IDItemSelected = item;
            List<lparser.config> configs = GlobalCfg.Instance.GetTableRow(item.ID);
            string ename = string.Empty;
            for (int i = 0; i < propertyList.Count; i++)
            {
                ename = propertyList[i].ename;
                fieldList.Add(new PropertyListItem()
                {
                    PropertyName = propertyList[i].cname,
                    EnName = propertyList[i].ename,
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
                        DataGridCell dataGridCell = GetCell(propertyDataGrid, a, j + 3);
                        dataGridCell.Background = Brushes.LightBlue;
                    }
                    if (trd.modifiedcells != null && trd.addedcells.ContainsKey(fieldList[a].EnName))
                    {
                        DataGridCell dataGridCell = GetCell(propertyDataGrid, a, j + 3);
                        dataGridCell.Background = Brushes.LightPink;
                    }
                    if (trd.modifiedcells != null && trd.deletedcells.ContainsKey(fieldList[a].EnName))
                    {
                        DataGridCell dataGridCell = GetCell(propertyDataGrid, a, j + 3);
                        dataGridCell.Background = Brushes.LightBlue;
                    }
                }
            }
        }

        #region WPF DataGrid控件获取DataGridCell方法
        private DataGridCell GetCell(DataGrid dataGrid, int rowIndex, int columnIndex)
        {
            DataGridRow rowContainer = GetRow(dataGrid, rowIndex);
            if (rowContainer != null)
            {
                DataGridCellsPresenter presenter = GetVisualChild<DataGridCellsPresenter>(rowContainer);
                DataGridCell cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex);
                if (cell == null)
                {
                    dataGrid.ScrollIntoView(rowContainer, dataGrid.Columns[columnIndex]);
                    cell = (DataGridCell)presenter.ItemContainerGenerator.ContainerFromIndex(columnIndex);
                }
                return cell;
            }
            return null;
        }

        private DataGridRow GetRow(DataGrid dataGrid, int rowIndex)
        {
            DataGridRow rowContainer = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
            if (rowContainer == null)
            {
                dataGrid.UpdateLayout();
                dataGrid.ScrollIntoView(dataGrid.Items[rowIndex]);
                rowContainer = (DataGridRow)dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex);
            }
            return rowContainer;
        }

        public T GetVisualChild<T>(Visual parent) where T : Visual
        {
            T child = default(T);
            int numVisuals = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < numVisuals; i++)
            {
                Visual v = (Visual)VisualTreeHelper.GetChild(parent, i);
                child = v as T;
                if (child == null)
                {
                    child = GetVisualChild<T>(v);
                }
                if (child != null)
                {
                    break;
                }
            }
            return child;
        }
        #endregion

        private void CheckStateBtn_Click(object sender, RoutedEventArgs e)
        {
            Dictionary<string, FileStatusStr> statusDic = SVNHelper.AllStatus();
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
                    RevertAll(_fileItemChoosed.Paths);
                    CheckStateBtn_Click(null, null);
                    GlobalCfg.Instance.ClearCurrent();
                    FileListView_SelectionChange(null, null);
                    break;
                case STATE_EDIT:
                    //请求进入编辑状态
                    if (SVNHelper.RequestEdit(_fileItemChoosed.FilePath))
                    {
                        _fileItemChoosed.IsEditing = true;
                    }
                    else
                    {
                        _fileItemChoosed.IsEditing = false;
                    }
                    JudgeMultiFuncBtnState();
                    ResetGenBtnEnable();
                    break;
                case STATE_FINISH_EDIT:
                    for (int i = 0; i < GlobalCfg.BranchCount; i++)
                    {
                        GlobalCfg.Instance.ExcuteModified(i);
                    }
                    ReleaseExcelRelative(_fileItemChoosed.FilePath);
                    _fileItemChoosed.IsEditing = false;
                    JudgeMultiFuncBtnState();
                    break;
                default:
                    break;
            }
        }

        private void CancelEdit_Click(object sender, RoutedEventArgs e)
        {
            GlobalCfg.Instance.ClearCurrent();
            FileListView_SelectionChange(null, null);
            _fileItemChoosed.IsEditing = false;
            JudgeMultiFuncBtnState();
        }

        private void ResetGenBtnEnable()
        {
            for(int i = 0; i < GenBtns.Count; i++)
            {
                GenBtns[i].IsEnabled = _fileItemChoosed.IsEditing;
            }
        }

        private void ResetGenBtnState()
        {
            for(int i = 0; i < GenBtns.Count; i++)
            {
                if (_IDItemSelected != null && _IDItemSelected.IsApplys[i])
                {
                    GenBtns[i].Content = STATE_CANCEL;
                }
                else
                {
                    GenBtns[i].Content = STATE_GEN;
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
            else if (_fileItemChoosed != null && !_fileItemChoosed.IsSame)
            {
                multiFunctionBtn.Content = STATE_REVERT;
            }
            //可请求进入编辑状态
            else if (_fileItemChoosed != null && _fileItemChoosed.IsSame && !_fileItemChoosed.IsEditing)
            {
                multiFunctionBtn.Content = STATE_EDIT;
            }
            else if (_fileItemChoosed != null && _fileItemChoosed.IsEditing)
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
            if(_fileItemChoosed != null && _fileItemChoosed.IsEditing)
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
            ReleaseAll();
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

    }
}