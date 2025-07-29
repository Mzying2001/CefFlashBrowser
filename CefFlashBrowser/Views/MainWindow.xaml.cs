using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using SimpleMvvm.Messaging;
using System.ComponentModel;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            WindowSizeInfo.Apply(GlobalData.Settings.MainWindowSizeInfo, this);

            Messenger.Global.Register(MessageTokens.CLOSE_MAINWINDOW, CloseMainWindowHandler);
            Closed += delegate { Messenger.Global.Unregister(MessageTokens.CLOSE_MAINWINDOW, CloseMainWindowHandler); };
        }

        private void CloseMainWindowHandler(object msg)
        {
            Close();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (!e.Cancel)
            {
                GlobalData.Settings.MainWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
        }
    }
}
