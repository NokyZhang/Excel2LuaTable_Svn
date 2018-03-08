using ExcelTools.Scripts.Lua;
using ExcelTools.Scripts.UserException;
using Lua;
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;


namespace ExcelTools.Scripts.UI
{
    /// <summary>
    /// PropertyEditWindow.xaml 的交互逻辑
    /// </summary>
    public partial class PropertyEditWindow : Window
    {
        private PropertyListItem propertyListItem;
        private string propertyName;
        private string oldpropertyContent;
        private string newpropertyContent;
        private string propertyId;
        private string cfgId;
        private int branchIndex;
        
        public PropertyEditWindow(PropertyListItem _propertyListItem, string _cfgId, int _branchIndex)
        {
            InitializeComponent();
            propertyListItem = _propertyListItem;
            propertyName = propertyListItem.PropertyName;
            newpropertyContent = oldpropertyContent = propertyListItem.GetBranchValue(_branchIndex);
            propertyId = propertyListItem.EnName;
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
            System.Windows.Forms.DialogResult dr = System.Windows.Forms.MessageBox.Show("是否确认对 " + propertyName + " 的修改？", "确认", buttons);
            if (dr == System.Windows.Forms.DialogResult.OK)
            {
                if (GlobalCfg.Instance.GetCurProperty(cfgId, propertyListItem.EnName, -1).type == lparser.PROPERTY_TYPE_TABLE) /*table需要语法检查*/
                {
                    if(newpropertyContent != "_EmptyTable")
                    {
                        string tableContent = "{" + newpropertyContent + "}";
                        byte[] byteArray = Encoding.UTF8.GetBytes(tableContent);
                        MemoryStream stream = new MemoryStream(byteArray);
                        StreamReader streamReader = new StreamReader(stream);
                        try
                        {
                            lgrammar_table.lgrammar(streamReader);
                        }
                        catch (LuaTableException ex)
                        {
                            System.Windows.Forms.MessageBoxButtons button = System.Windows.Forms.MessageBoxButtons.OK;
                            System.Windows.Forms.DialogResult errorDr = System.Windows.Forms.MessageBox.Show(ex.Message, "错误", button);
                            if (errorDr == System.Windows.Forms.DialogResult.OK)
                            {
                                return;
                            }
                        }
                    }
                }
                GlobalCfg.Instance.SetCurProperty(cfgId, propertyId, branchIndex, newpropertyContent);
                oldpropertyContent = newpropertyContent;
                propertyListItem.SetBranchValue(branchIndex, newpropertyContent);
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
                System.Windows.Forms.DialogResult dr = System.Windows.Forms.MessageBox.Show("是否放弃对 " + propertyName + " 的修改？", "确认", buttons);
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
