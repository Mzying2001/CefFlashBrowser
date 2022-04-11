using SimpleMvvm.Command;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Views.Dialogs.JsDialogs
{
    /// <summary>
    /// JsConfirmDialog.xaml 的交互逻辑
    /// </summary>
    public partial class JsConfirmDialog : Window
    {


        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Message.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(JsConfirmDialog), new PropertyMetadata(string.Empty));


        private bool? _result = null;
        private Action<bool?> _callback;

        public ICommand YesCommand { get; }
        public ICommand NoCommand { get; }

        public JsConfirmDialog()
        {
            YesCommand = new DelegateCommand(() =>
            {
                _result = true;
                Close();
            });

            NoCommand = new DelegateCommand(() =>
            {
                _result = false;
                Close();
            });

            InitializeComponent();
        }

        private void JsConfirmDialog_Closing(object sender, CancelEventArgs e)
        {
            _callback?.Invoke(_result);
        }

        public static void ShowDialog(string message, string title = null, Action<bool?> callback = null)
        {
            new JsConfirmDialog
            {
                Title = title ?? string.Empty,
                Message = message,
                _callback = callback
            }.ShowDialog();
        }
    }
}
