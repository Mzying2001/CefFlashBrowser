using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Views.Custom;
using CefFlashBrowser.Views.Dialogs;
using SimpleMvvm.Messaging;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : DropableWindow
    {
        private static Window window = null;

        public MainWindow()
        {
            if (GlobalData.Settings.FirstStart)
            {
                new SelectLanguageDialog().ShowDialog();
                GlobalData.Settings.FirstStart = false;
            }

            InitializeComponent();

            Messenger.Global.Register(MessageTokens.LANGUAGE_CHANGED, UpdateLanguageMenuChecked);
        }

        private void UpdateLanguageMenuChecked(object obj)
        {
            if (obj is string current)
            {
                foreach (var item in languageMenu.Items)
                {
                    MenuItem menuItem = (MenuItem)languageMenu.ItemContainerGenerator.ContainerFromItem(item);
                    menuItem.IsChecked = (string)item == current;
                }
            }
        }

        private void MainWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            window = null;
            Messenger.Global.Unregister(MessageTokens.LANGUAGE_CHANGED, UpdateLanguageMenuChecked);
        }

        public static new void Show()
        {
            if (window == null)
            {
                window = new MainWindow();
                window.Show();
            }
            else
            {
                window.Activate();
            }
        }
    }
}
