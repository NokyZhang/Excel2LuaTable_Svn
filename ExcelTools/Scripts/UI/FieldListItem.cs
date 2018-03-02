
using System.ComponentModel;

namespace ExcelTools.Scripts.UI
{
    class PropertyListItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public bool IsNeedGen { get; set; }

        public string PropertyName { get; set; }

        public string EnName { get; set; }

        public string LocalContent { get; set; }

        private string _Trunk;
        public string Trunk
        {
            get { return _Trunk; }
            set
            {
                _Trunk = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Trunk"));
            }
        }

        private string _Studio;
        public string Studio
        {
            get { return _Studio; }
            set
            {
                _Studio = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Studio"));
            }
        }

        private string _TF;
        public string TF
        {
            get { return _TF; }
            set
            {
                _TF = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("TF"));
            }
        }

        private string _Release;
        public string Release
        {
            get { return _Release; }
            set
            {
                _Release = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Release"));
            }
        }
    }
}
