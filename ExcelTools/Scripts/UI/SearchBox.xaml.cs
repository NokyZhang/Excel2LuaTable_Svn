using ExcelTools.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ExcelTools.Scripts.UI
{
    /// <summary>
    /// SearchBox.xaml 的交互逻辑
    /// </summary>
    public partial class SearchBox : UserControl
    {

        public SearchBox()
        {
            InitializeComponent();
            KeyDown += Keyboard_OnClick;
        }

        private void Keyboard_OnClick(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter)
            {
                Btn.Focus();
                ExeccuteSearch();
            }
        }

        public event EventHandler<SearchEventArgs> OnSearch;
        private void BtnSearch_OnClick(object sender, RoutedEventArgs e)
        {
            ExeccuteSearch();
        }

        public event EventHandler<CancelSearchEventArgs> OnCancelSearch;
        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            CancelSearch();
        }

        private void TbxInput_OnKeyDown(object sender, KeyEventArgs e)
        {
            //ExeccuteSearch();
        }

        private void ExeccuteSearch()
        {
            if (OnSearch != null)
            {
                var args = new SearchEventArgs();
                args.SearchText = TbxInput.Text;
                OnSearch(this, args);
                ChangeBtn(BtnType.BTN_CANCEL);
            }
        }

        private void CancelSearch()
        {
            if(OnCancelSearch != null)
            {
                var args = new CancelSearchEventArgs();
                OnCancelSearch(this, args);
                TbxInput.Text = null;
                ChangeBtn(BtnType.BTN_SEARCH);
            }
        }

        private void ChangeBtn(BtnType btnType)
        {
            Image btnIcon = WPFHelper.GetVisualChild<Image>(Btn);
            switch (btnType)
            {
                case BtnType.BTN_CANCEL:
                    Btn.Click -= BtnSearch_OnClick;
                    Btn.Click += BtnCancel_OnClick;
                    BitmapImage closeIcon = new BitmapImage(new Uri("close.png", UriKind.Relative));
                    btnIcon.Source = closeIcon;
                    TbxInput.IsEnabled = false;
                    break;
                case BtnType.BTN_SEARCH:
                    Btn.Click -= BtnCancel_OnClick;
                    Btn.Click += BtnSearch_OnClick;
                    BitmapImage searchIcon = new BitmapImage(new Uri("search.png", UriKind.Relative));
                    btnIcon.Source = searchIcon;
                    TbxInput.IsEnabled = true;
                    break;
                default:
                    break;
            }
        }
    }
    public class SearchEventArgs : EventArgs
    {
        public string SearchText { get; set; }
    }

    public class CancelSearchEventArgs : EventArgs
    {
    }

    public enum BtnType
    {
        BTN_SEARCH = 0,
        BTN_CANCEL = 1,
    }
}
