using CefFlashBrowser.Models.Data;
using SimpleMvvm.Messaging;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs
{
    /// <summary>
    /// SelectLanguageDialog.xaml 的交互逻辑
    /// </summary>
    public partial class SelectLanguageDialog : Window
    {
        public SelectLanguageDialog()
        {
            InitializeComponent();

            Messenger.Global.Register(MessageTokens.EXIT_SELECTLANGUAGE, CloseWindow);
            Closing += (s, e) => Messenger.Global.Unregister(MessageTokens.EXIT_SELECTLANGUAGE, CloseWindow);
        }

        private void CloseWindow(object obj)
        {
            Close();
        }
    }
}
