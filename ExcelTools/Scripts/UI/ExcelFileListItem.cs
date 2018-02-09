using System.Collections.Generic;
using System.ComponentModel;

class ExcelFileListItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    #region ��ʾ���
    public string Name { get; set; }

    private bool _IsSame;
    //��SVN�ϵ������Ƿ���ͬ
    public bool IsSame {
        get { return _IsSame; }
        set {
            _IsSame = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("IsSame"));
        }
    }

    //�Ƿ��ڱ༭״̬
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

    //��SVN��ͬ��������õ�����·��
    public List<string> Paths;
    public string ClientServer { get; set; }
    //������Excel·��
    public string FilePath { get; set; }
}