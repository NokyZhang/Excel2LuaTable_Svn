
using System.Collections.Generic;
using System.ComponentModel;

namespace ExcelTools.Scripts.UI
{
    public class PropertyListItem : INotifyPropertyChanged
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

        public string GetBranchValue(int branchIndex)
        {
            switch(branchIndex)
            {
                case 0:
                    return Trunk;
                case 1:
                    return Studio;
                case 2:
                    return TF;
                case 3:
                    return Release;
                default:
                    return null;
            }
        }

        public void SetBranchValue(int branchIndex, string value)
        {
            switch (branchIndex)
            {
                case 0:
                    Trunk = value;
                    break;
                case 1:
                    Studio = value;
                    break;
                case 2:
                    TF = value;
                    break;
                case 3:
                    Release = value;
                    break;
                default:
                    break;
            }
        }
    }
}
