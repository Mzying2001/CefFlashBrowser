using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// SolSaveManager.xaml 的交互逻辑
    /// </summary>
    public partial class SolSaveManager : Window
    {
        public SolSaveManagerViewModel ViewModel
        {
            get => DataContext as SolSaveManagerViewModel;
        }

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

        private void ListViewItemMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListViewItem item)
            {
                Dispatcher.InvokeAsync(() =>
                    ViewModel?.CurrentWorkspace?.EditSolCommand.Execute(item.DataContext));
            }
        }
    }
}
