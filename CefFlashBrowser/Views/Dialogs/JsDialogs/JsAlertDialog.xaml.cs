using SimpleMvvm.Command;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Views.Dialogs.JsDialogs
{
    /// <summary>
    /// JsConfirmDialog.xaml 的交互逻辑
    /// </summary>
    public partial class JsAlertDialog : Window
    {


        public string Message
        {
            get { return (string)GetValue(MessageProperty); }
            set { SetValue(MessageProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Message.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register("Message", typeof(string), typeof(JsAlertDialog), new PropertyMetadata(string.Empty));


        public ICommand CloseCommand { get; }

        public JsAlertDialog()
        {
            CloseCommand = new DelegateCommand(Close);
            InitializeComponent();
        }

        public static void ShowDialog(string message, string title = "")
        {
            new JsAlertDialog
            {
                Title = title,
                Message = message
            }.ShowDialog();
        }
    }
}
