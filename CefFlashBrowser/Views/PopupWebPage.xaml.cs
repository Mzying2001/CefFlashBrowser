using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefSharp;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// PopupWebPage.xaml 的交互逻辑
    /// </summary>
    public partial class PopupWebPage : Window
    {
        private class PopWebPageLifeSpanHandler : LifeSpanHandler
        {
            private readonly PopupWebPage window;

            public PopWebPageLifeSpanHandler(PopupWebPage window)
            {
                this.window = window;
            }

            public override bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                window.Dispatcher.Invoke(delegate
                {
                    window._doClose = true;
                    window.Close();
                });
                return false;
            }

            public override bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
            {
                WindowManager.ShowBrowser(targetUrl);
                newBrowser = null;
                return true;
            }
        }

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

            browser.MenuHandler = new Utils.Handlers.ContextMenuHandler();
            browser.JsDialogHandler = new Utils.Handlers.JsDialogHandler();
            browser.DownloadHandler = new Utils.Handlers.IEDownloadHandler();
            browser.LifeSpanHandler = new PopWebPageLifeSpanHandler(this);
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!browser.IsDisposed && !_doClose)
            {
                bool forceClose = GlobalData.Settings.DisableOnBeforeUnloadDialog;
                browser.GetBrowser().CloseBrowser(forceClose);
                e.Cancel = true;
            }
        }
    }
}