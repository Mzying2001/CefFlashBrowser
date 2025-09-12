using CefFlashBrowser.Utils;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs
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
            DependencyProperty.Register("Message", typeof(string), typeof(JsPromptDialog), new PropertyMetadata(null));


        public string InputText
        {
            get { return (string)GetValue(InputTextProperty); }
            set { SetValue(InputTextProperty, value); }
        }

        // Using a DependencyProperty as the backing store for InputText.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty InputTextProperty =
            DependencyProperty.Register("InputText", typeof(string), typeof(JsPromptDialog), new PropertyMetadata(null));


        public JsPromptDialog()
        {
            InitializeComponent();
        }

        private void OnCancelButtonClick(object sender, RoutedEventArgs e)
        {
            DialogHelper.SetDialogResult(this, false);
        }

        private void OnOkButtonClick(object sender, RoutedEventArgs e)
        {
            DialogHelper.SetDialogResult(this, true);
        }
    }
}
