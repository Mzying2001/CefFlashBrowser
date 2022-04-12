using CefFlashBrowser.FlashBrowser.Handlers;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// ViewSourceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ViewSourceWindow : Window
    {


        public string Address
        {
            get { return (string)GetValue(AddressProperty); }
            set { SetValue(AddressProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Address.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AddressProperty =
            DependencyProperty.Register("Address", typeof(string), typeof(ViewSourceWindow), new PropertyMetadata(null, (s, e) =>
            {
                var window = (ViewSourceWindow)s;
                window.browser.Load($"view-source:{e.NewValue}");
            }));


        public ViewSourceWindow()
        {
            InitializeComponent();

            Closing += (s, e) =>
            {
                browser.GetBrowser().CloseBrowser(true);
            };
        }

        public ViewSourceWindow(string address) : this()
        {
            Address = address;
        }

        public static void Show(string address)
        {
            new ViewSourceWindow(address).Show();
        }

        private void OnCreateNewBrowser(object sender, LifeSpanHandler.NewBrowserEventArgs e)
        {
            e.Handled = true;
            BrowserWindow.Show(e.TargetUrl);
        }
    }
}
