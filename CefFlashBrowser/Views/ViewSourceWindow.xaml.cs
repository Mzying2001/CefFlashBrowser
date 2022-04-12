using CefFlashBrowser.FlashBrowser.Handlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
                browser.Dispose();
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

        private void Browser_OnCreateNewWindow(object sender, LifeSpanHandler.NewWindowEventArgs e)
        {
            e.CancelPopup = true;
            BrowserWindow.Show(e.TargetUrl);
        }
    }
}
