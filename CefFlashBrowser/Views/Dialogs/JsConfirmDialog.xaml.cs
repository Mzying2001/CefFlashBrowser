using CefFlashBrowser.Utils;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs
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
            DependencyProperty.Register("Message", typeof(string), typeof(JsConfirmDialog), new PropertyMetadata(null));


        public JsConfirmDialog()
        {
            InitializeComponent();
        }

        private void OnNoButtonClick(object sender, RoutedEventArgs e)
        {
            DialogHelper.SetDialogResult(this, false);
        }

        private void OnYesButtonClick(object sender, RoutedEventArgs e)
        {
            DialogHelper.SetDialogResult(this, true);
        }
    }
}
