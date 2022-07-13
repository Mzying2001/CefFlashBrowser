using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefSharp;
using System;
using System.Net;
using System.Windows;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// SwfPlayerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SwfPlayerWindow : Window
    {
        private class SwfPlayerLifeSpanHandler : LifeSpanHandler
        {
            private readonly SwfPlayerWindow window;

            public SwfPlayerLifeSpanHandler(SwfPlayerWindow window)
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
                Application.Current.Dispatcher.Invoke(() => WindowManager.ShowBrowser(targetUrl));
                newBrowser = null;
                return true;
            }
        }

        private bool _doClose = false;


        public string FileName
        {
            get { return (string)GetValue(FileNameProperty); }
            set { SetValue(FileNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FileName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register("FileName", typeof(string), typeof(SwfPlayerWindow), new PropertyMetadata(string.Empty, OnFileNamePropertyChanged));

        private static void OnFileNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is SwfPlayerWindow swfPlayerWindow)
            {
                swfPlayerWindow.browser.Address = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                    $"Assets/swfplayer.html?src={WebUtility.UrlEncode((string)e.NewValue)}");
            }
        }


        public SwfPlayerWindow()
        {
            InitializeComponent();

            WindowSizeInfo.Apply(GlobalData.Settings.SwfWindowSizeInfo, this);

            browser.DragHandler = new Utils.Handlers.DisableDragHandler();
            browser.MenuHandler = new Utils.Handlers.ContextMenuHandler();
            browser.JsDialogHandler = new Utils.Handlers.JsDialogHandler();
            browser.DownloadHandler = new Utils.Handlers.IEDownloadHandler();
            browser.LifeSpanHandler = new SwfPlayerLifeSpanHandler(this);
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (browser.IsDisposed || _doClose)
            {
                GlobalData.Settings.SwfWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
            else
            {
                bool forceClose = GlobalData.Settings.DisableOnBeforeUnloadDialog;
                browser.GetBrowser().CloseBrowser(forceClose);
                e.Cancel = true;
            }
        }
    }
}
