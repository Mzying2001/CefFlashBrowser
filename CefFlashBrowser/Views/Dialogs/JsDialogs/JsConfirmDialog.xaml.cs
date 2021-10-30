using CefFlashBrowser.Models;
using CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels;
using SimpleMvvm.Messaging;
using System;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs.JsDialogs
{
    /// <summary>
    /// JsConfirmDialog.xaml 的交互逻辑
    /// </summary>
    public partial class JsConfirmDialog : Window
    {
        private bool? result = null;
        private Action<bool?> callback;

        public JsConfirmDialog()
        {
            InitializeComponent();

            Messenger.Global.Register(MessageTokens.EXIT_JSCONFIRM, CloseWindow);
            Closing += JsConfirmDialog_Closing;
        }

        private void JsConfirmDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            callback?.Invoke(result);
            Messenger.Global.Unregister(MessageTokens.EXIT_JSCONFIRM, CloseWindow);
        }

        private void CloseWindow(object obj)
        {
            result = (bool?)obj;
            Close();
        }

        public static void Show(string message, string title = "", Action<bool?> callback = null)
        {
            var dialog = new JsConfirmDialog { callback = callback };
            var vmodel = (JsConfirmDialogViewModel)dialog.DataContext;

            vmodel.Message = message;
            vmodel.Title = title;
            dialog.ShowDialog();
        }
    }
}
