using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Utils;
using CefSharp;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// ViewSourceWindow.xaml 的交互逻辑
    /// </summary>
    public partial class ViewSourceWindow : Window
    {
        private class ViewSourceBrowserLifeSpanHandler : LifeSpanHandler
        {
            public override bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition, bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo, IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
            {
                Application.Current.Dispatcher.Invoke(() => WindowManager.ShowBrowser(targetUrl));
                newBrowser = null;
                return true;
            }
        }


        public string Address
        {
            get { return (string)GetValue(AddressProperty); }
            set { SetValue(AddressProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Address.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty AddressProperty =
            DependencyProperty.Register("Address", typeof(string), typeof(ViewSourceWindow), new PropertyMetadata(null, (d, e) =>
            {
                ((ViewSourceWindow)d).browser.Address = $"view-source:{e.NewValue}";
            }));


        public ViewSourceWindow()
        {
            InitializeComponent();

            browser.DragHandler = new Utils.Handlers.DisableDragHandler();
            browser.MenuHandler = new Utils.Handlers.ContextMenuHandler();
            browser.JsDialogHandler = new Utils.Handlers.JsDialogHandler();
            browser.DownloadHandler = new Utils.Handlers.IEDownloadHandler();
            browser.LifeSpanHandler = new ViewSourceBrowserLifeSpanHandler();

            Closing += (s, e) =>
            {
                browser.GetBrowser().CloseBrowser(true);
            };
        }
    }
}
