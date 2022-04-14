using CefFlashBrowser.Models.Data;
using System;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// PopupWebPage.xaml 的交互逻辑
    /// </summary>
    public partial class PopupWebPage : Window
    {
        private bool _doClose;


        public string Address
        {
            get { return (string)GetValue(AddressProperty); }
            set { SetValue(AddressProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Address.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AddressProperty =
            DependencyProperty.Register("Address", typeof(string), typeof(PopupWebPage), new PropertyMetadata(null, (d, e) =>
            {
                PopupWebPage window = (PopupWebPage)d;
                window.browser.Address = (string)e.NewValue;
            }));


        public PopupWebPage()
        {
            InitializeComponent();
        }

        public PopupWebPage(string address) : this()
        {
            Address = address;
        }

        public static void Show(string address)
        {
            new PopupWebPage(address).Show();
        }

        public static void Show(string address, CefSharp.IPopupFeatures popupFeatures)
        {
            new PopupWebPage(address)
            {
                Left = popupFeatures.X,
                Top = popupFeatures.Y,
                Width = popupFeatures.Width,
                Height = popupFeatures.Height
            }.Show();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_doClose)
            {
                bool forceClose = GlobalData.Settings.DisableOnBeforeUnloadDialog;
                browser.GetBrowser().CloseBrowser(forceClose);
                e.Cancel = true;
            }
        }

        private void BrowserOnClose(object sender, EventArgs e)
        {
            _doClose = true;
            Close();
        }

        private void OnCreateNewBrowser(object sender, FlashBrowser.Handlers.LifeSpanHandler.NewBrowserEventArgs e)
        {
            e.Handled = true;
            BrowserWindow.Show(e.TargetUrl);
        }
    }
}