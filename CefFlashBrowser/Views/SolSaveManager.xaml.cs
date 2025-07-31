using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using System.ComponentModel;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// SolSaveManager.xaml 的交互逻辑
    /// </summary>
    public partial class SolSaveManager : Window
    {
        public SolSaveManager()
        {
            InitializeComponent();
            WindowSizeInfo.Apply(GlobalData.Settings.SolSaveManagerSizeInfo, this);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!e.Cancel)
            {
                GlobalData.Settings.SolSaveManagerSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
        }
    }
}
