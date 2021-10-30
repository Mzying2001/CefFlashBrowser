using CefFlashBrowser.Models;
using CefFlashBrowser.ViewModels.DialogViewModels.JsDialogViewModels;
using SimpleMvvm.Messaging;
using System;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs.JsDialogs
{
    /// <summary>
    /// JsPromptDialog.xaml 的交互逻辑
    /// </summary>
    public partial class JsPromptDialog : Window
    {
        private string result = null;
        private Action<string> callback;

        public JsPromptDialog()
        {
            InitializeComponent();

            Messenger.Global.Register(MessageTokens.EXIT_JSPROMPT, CloseWindow);
            Closing += JsPromptDialog_Closing;
        }

        private void JsPromptDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            callback?.Invoke(result);
            Messenger.Global.Unregister(MessageTokens.EXIT_JSPROMPT, CloseWindow);
        }

        private void CloseWindow(object obj)
        {
            result = (string)obj;
            Close();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            inputBox.Focus();
            inputBox.SelectAll();
        }

        public static void Show(string message, string title = "", string defaulePromptText = "", Action<string> callback = null)
        {
            var dialog = new JsPromptDialog { callback = callback };
            var vmodel = (JsPromptDialogViewModel)dialog.DataContext;

            vmodel.Message = message;
            vmodel.Title = title;
            vmodel.PromptText = defaulePromptText;
            dialog.ShowDialog();
        }
    }
}
