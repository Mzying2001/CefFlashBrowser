using CefFlashBrowser.Models.Data;
using SimpleMvvm.Messaging;
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

            Messenger.Global.Register(MessageTokens.CLOSE_MAINWINDOW, CloseMainWindowHandler);
            Closed += delegate { Messenger.Global.Unregister(MessageTokens.CLOSE_MAINWINDOW, CloseMainWindowHandler); };
        }

        private void CloseMainWindowHandler(object obj)
        {
            Close();
        }
    }
}
