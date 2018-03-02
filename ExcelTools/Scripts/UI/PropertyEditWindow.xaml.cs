using Lua;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using System.Windows.Shapes;

namespace ExcelTools.Scripts.UI
{
    /// <summary>
    /// PropertyEditWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PropertyEditWindow : Window
    {
        private string propertyName;
        private string oldpropertyContent;
        private string newpropertyContent;
        private string propertyId;
        private string cfgId;
        private int branchIndex;
        
        public PropertyEditWindow(string _propertyName, string _propertyId, string _propertyContent, string _cfgId, int _branchIndex)
        {
            InitializeComponent();
            propertyName = _propertyName;
            newpropertyContent = oldpropertyContent = _propertyContent;
            propertyId = _propertyId;
            cfgId = _cfgId;
            branchIndex = _branchIndex;
            Init();
        }

        private void Init()
        {
            Title = propertyName;
            propertyTextBox.Text = oldpropertyContent;
            propertyTextBox.TextChanged += OnTextChange;
        }

        private void OnTextChange(object sender, TextChangedEventArgs e)
        {
            newpropertyContent = propertyTextBox.Text;
            RefreshConfirmBtnState();
        }

        private void RefreshConfirmBtnState()
        {
            if(newpropertyContent != oldpropertyContent)
            {
                ConfirmBtn.IsEnabled = true;
            }
            else
            {
                ConfirmBtn.IsEnabled = false;
            }
        }

        private void ConfirmBtn_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.OKCancel;
            System.Windows.Forms.DialogResult dr = System.Windows.Forms.MessageBox.Show("是否将" + oldpropertyContent + "修改为" + newpropertyContent + "？", "确认", buttons);
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                lparser.property property = GlobalCfg.Instance.GetProperty(cfgId, propertyId, branchIndex);
                property.value = newpropertyContent;
                oldpropertyContent = newpropertyContent;
                //TODO:修改FieldListItem对应属性，没有此property的要去生成
                Close();
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (newpropertyContent != oldpropertyContent)
            {
                System.Windows.Forms.MessageBoxButtons buttons = System.Windows.Forms.MessageBoxButtons.OKCancel;
                System.Windows.Forms.DialogResult dr = System.Windows.Forms.MessageBox.Show("是否放弃" + propertyName + "的修改？", "确认", buttons);
                if (dr == System.Windows.Forms.DialogResult.OK)
                {
                    base.OnClosing(e);
                }
                else
                {
                    e.Cancel = true;
                }
            }
            else
            {
                base.OnClosing(e);
            }
        }
    }
}
