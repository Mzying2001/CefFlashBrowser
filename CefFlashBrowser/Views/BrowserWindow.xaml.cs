using CefFlashBrowser.FlashBrowser;
using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// BrowserWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BrowserWindow : Window
    {
        private bool _doClose = false;

        public BrowserWindow()
        {
            InitializeComponent();

            if (GlobalData.Settings.BrowserWindowSizeInfo != null)
                GlobalData.Settings.BrowserWindowSizeInfo.Apply(this);

            browser.KeyboardHandler = new BrowserKeyboardHandler(browser);
        }

        private void BrowserLoadingStateChanged(object sender, CefSharp.LoadingStateChangedEventArgs e)
        {
            var wfChromiumWebBrowser = (WfChromiumWebBrowser)sender;
            if (wfChromiumWebBrowser.IsLoading)
            {
                Title = LanguageManager.GetString("label_loading");
            }
            else
            {
                Title = wfChromiumWebBrowser.Title ?? string.Empty;
            }
        }

        private void BrowserTitleChanged(object sender, CefSharp.TitleChangedEventArgs e)
        {
            var wfChromiumWebBrowser = (WfChromiumWebBrowser)sender;
            if (!wfChromiumWebBrowser.IsLoading)
            {
                Title = e.Title ?? string.Empty;
            }
        }

        private void OnCreateNewBrowser(object sender, LifeSpanHandler.NewBrowserEventArgs e)
        {
            e.Handled = true;

            if (e.OpenDisposition == CefSharp.WindowOpenDisposition.NewPopup)
            {
                PopupWebPage.Show(e.TargetUrl, e.PopupFeatures);
            }
            else
            {
                switch (GlobalData.Settings.NewPageBehavior)
                {
                    case NewPageBehavior.NewWindow:
                        {
                            Show(e.TargetUrl);
                            break;
                        }
                    case NewPageBehavior.OriginalWindow:
                        {
                            browser.Load(e.TargetUrl);
                            break;
                        }
                }
            }
        }

        private void BrowserOnClose(object sender, System.EventArgs e)
        {
            _doClose = true;
            Close();
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_doClose)
            {
                browserWindow.WindowState = WindowState.Normal;
                GlobalData.Settings.BrowserWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
            else
            {
                bool forceClose = GlobalData.Settings.DisableOnBeforeUnloadDialog;
                browser.GetBrowser().CloseBrowser(forceClose);
                e.Cancel = true;
            }
        }

        private void MenuButtonClicked(object sender, RoutedEventArgs e)
        {
            var contextMenu = (ContextMenu)Resources["more"];
            contextMenu.PlacementTarget = sender as UIElement;
            contextMenu.Placement = PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }

        private void ShowBlockedSwfsButtonClicked(object sender, RoutedEventArgs e)
        {
            var contextMenu = (ContextMenu)Resources["blockedSwfs"];
            contextMenu.PlacementTarget = sender as UIElement;
            contextMenu.Placement = PlacementMode.Bottom;
            contextMenu.IsOpen = true;
        }

        public static void Show(string address)
        {
            var window = new BrowserWindow();
            window.browser.Load(address);
            window.Show();
        }
    }
}
