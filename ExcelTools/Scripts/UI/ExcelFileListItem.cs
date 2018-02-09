using System.Collections.Generic;
using System.ComponentModel;

class ExcelFileListItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    #region 显示相关
    public string Name { get; set; }

    private bool _IsSame;
    //与SVN上的内容是否相同
    public bool IsSame {
        get { return _IsSame; }
        set {
            _IsSame = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSame"));
        }
    }

    //是否处于编辑状态
    private bool _IsEditing;
    public bool IsEditing
    {
        get { return _IsEditing; }
        set
        {
            _IsEditing = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsEditing"));
        }
    }
    #endregion

    //与SVN不同的相关配置的所有路径
    public List<string> Paths;
    public string ClientServer { get; set; }
    //真正的Excel路径
    public string FilePath { get; set; }
}