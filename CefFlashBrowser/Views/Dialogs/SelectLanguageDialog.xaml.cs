using CefFlashBrowser.Utils;
using SimpleMvvm.Command;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Views.Dialogs
{
    /// <summary>
    /// SelectLanguageDialog.xaml 的交互逻辑
    /// </summary>
    public partial class SelectLanguageDialog : Window
    {


        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Header.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(SelectLanguageDialog), new PropertyMetadata(null));


        public ICommand SetHeaderCommand { get; }
        public ICommand SelectLanguageCommand { get; }


        public SelectLanguageDialog()
        {
            SetHeaderCommand = new DelegateCommand<string>(header => Header = header);

            SelectLanguageCommand = new DelegateCommand<string>(language =>
            {
                LanguageManager.CurrentLanguage = language;
                Close();
            });

            InitializeComponent();
        }
    }
}
