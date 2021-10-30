using CefFlashBrowser.Models;
using CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels;
using SimpleMvvm.Messaging;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs.JsDialogs
{
    /// <summary>
    /// JsConfirmDialog.xaml 的交互逻辑
    /// </summary>
    public partial class JsAlertDialog : Window
    {
        public JsAlertDialog()
        {
            InitializeComponent();

            Messenger.Global.Register(MessageTokens.EXIT_JSALERT, CloseWindow);
            Closing += (s, e) => Messenger.Global.Unregister(MessageTokens.EXIT_JSALERT, CloseWindow);
        }

        private void CloseWindow(object obj)
        {
            Close();
        }

        public static void Show(string message, string title = "")
        {
            var dialog = new JsAlertDialog();
            var vmodel = (JsAlertDialogViewModel)dialog.DataContext;

            vmodel.Message = message;
            vmodel.Title = title;
            dialog.ShowDialog();
        }
    }
}
