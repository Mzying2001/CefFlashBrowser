using SimpleMvvm.Command;
using System;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Views.Dialogs.JsDialogs
{
    /// <summary>
    /// JsPromptDialog.xaml 的交互逻辑
    /// </summary>
    public partial class JsPromptDialog : Window
    {


        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Message.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(JsPromptDialog), new PropertyMetadata(string.Empty));


        public string Input
        {
            get { return (string)GetValue(InputProperty); }
            set { SetValue(InputProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Input.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InputProperty =
            DependencyProperty.Register("Input", typeof(string), typeof(JsPromptDialog), new PropertyMetadata(string.Empty));


        private string _result = null;
        private Action<string> _callback;

        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        public JsPromptDialog()
        {
            OkCommand = new DelegateCommand(() =>
            {
                _result = Input;
                Close();
            });

            CancelCommand = new DelegateCommand(() =>
            {
                _result = null;
                Close();
            });

            InitializeComponent();
        }

        private void JsPromptDialog_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _callback?.Invoke(_result);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            inputBox.Focus();
            inputBox.SelectAll();
        }

        public static void ShowDialog(string message, string title = null, string defaulePromptText = null, Action<string> callback = null)
        {
            new JsPromptDialog
            {
                Title = title ?? string.Empty,
                Message = message,
                Input = defaulePromptText,
                _callback = callback
            }.ShowDialog();
        }
    }
}
