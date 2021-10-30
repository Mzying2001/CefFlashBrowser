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

            string token = MessageTokens.CreateToken(MessageTokens.CLOSE_WINDOW, typeof(JsConfirmDialogViewModel));
            Messenger.Global.Register(token, CloseWindow);
            Closing += (s, e) =>
            {
                callback?.Invoke(result);
                Messenger.Global.Unregister(token, CloseWindow);
            };
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
