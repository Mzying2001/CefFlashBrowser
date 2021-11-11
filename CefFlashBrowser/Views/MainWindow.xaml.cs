using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Views.Custom;
using CefFlashBrowser.Views.Dialogs;
using SimpleMvvm.Messaging;
using System.Windows.Controls;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : DropableWindow
    {
        public MainWindow()
        {
            InitializeComponent();

            Messenger.Global.Register(MessageTokens.LANGUAGE_CHANGED, UpdateLanguageMenuChecked);
            Closing += (s, e) => Messenger.Global.Unregister(MessageTokens.LANGUAGE_CHANGED, UpdateLanguageMenuChecked);

            if (GlobalData.Settings.FirstStart)
            {
                new SelectLanguageDialog().ShowDialog();
                GlobalData.Settings.FirstStart = false;
            }
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


    }
}
