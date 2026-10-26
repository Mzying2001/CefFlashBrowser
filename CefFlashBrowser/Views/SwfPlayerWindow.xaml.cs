using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Ioc;
using SimpleMvvm.Messaging;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// SwfPlayerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SwfPlayerWindow : Window
    {
        private bool _doClose = false;
        private bool _isClosed = false;
        private ChromiumWebBrowser _browserPendingClose;
        private bool _forceClosePending;

        private BrowserViewModel ViewModel => browserView?.ViewModel;

        public string FileName
        {
            get => (string)GetValue(FileNameProperty);
            set => SetValue(FileNameProperty, value);
        }

        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register(nameof(FileName), typeof(string), typeof(SwfPlayerWindow), new PropertyMetadata(string.Empty, OnFileNamePropertyChanged));

        private static void OnFileNamePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = (SwfPlayerWindow)d;
            var browser = window.ViewModel?.Browser;

            if (window._isClosed || !window.browserView.IsLoaded
                || browser == null || browser.IsDisposed)
            {
                return;
            }

            try
            {
                if (browser.CanExecuteJavascriptInMainFrame)
                {
                    window.LoadSwf(e.NewValue as string);
                }
            }
            catch (ObjectDisposedException)
            {
                // CEF can dispose the browser between the state check and this query.
            }
        }

        public SwfPlayerWindow()
        {
            InitializeComponent();
            WindowSizeInfo.Apply(GlobalData.Settings.SwfWindowSizeInfo, this);

            browserView.ViewModel = SimpleIoc.Global.GetInstance<BrowserViewModel>(GlobalData.SwfPlayerPath);
            ViewModel.Browser.FrameLoadEnd += OnBrowserFrameLoadEnd;
            ViewModel.Browser.DragHandler = new Browser.Handlers.DisableDragHandler();

            Messenger.Global.Register(MessageTokens.CLOSE_ALL_BROWSERS, CloseBrowserHandler);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var browser = ViewModel?.Browser;

            if (_doClose || browser == null || browser.IsDisposed)
            {
                GlobalData.Settings.SwfWindowSizeInfo = WindowSizeInfo.GetSizeInfo(this);
            }
            else if (browser is ChromiumWebBrowser wpfBrowser && !browser.IsBrowserInitialized)
            {
                DeferCloseUntilBrowserInitialized(wpfBrowser);
                e.Cancel = true;
            }
            else
            {
                try
                {
                    var cefBrowser = browser.GetBrowser();
                    if (cefBrowser != null)
                    {
                        cefBrowser.CloseBrowser(_forceClosePending || GlobalData.Settings.DisableOnBeforeUnloadDialog);
                        e.Cancel = true;
                    }
                    else
                    {
                        _doClose = true;
                    }
                }
                catch (ObjectDisposedException)
                {
                    _doClose = true;
                }
            }
            base.OnClosing(e);
        }

        private void DeferCloseUntilBrowserInitialized(ChromiumWebBrowser browser)
        {
            if (_browserPendingClose == browser)
                return;

            _browserPendingClose = browser;
            browser.IsBrowserInitializedChanged += BrowserInitializedForClose;
        }

        private void BrowserInitializedForClose(object sender, EventArgs e)
        {
            var browser = _browserPendingClose;

            if (browser == null || !browser.IsBrowserInitialized)
                return;

            browser.IsBrowserInitializedChanged -= BrowserInitializedForClose;
            _browserPendingClose = null;

            // The initialized notification can reach WPF before CEF has returned
            // from OnAfterCreated. Wait for that callback before destroying its HWND.
            InvokeAfterCefCallback(() =>
            {
                if (!_isClosed)
                {
                    if (_forceClosePending)
                    {
                        try
                        {
                            browser.GetBrowser()?.CloseBrowser(true);
                        }
                        catch (ObjectDisposedException)
                        {
                            _doClose = true;
                            Close();
                        }
                    }
                    else
                    {
                        Close();
                    }
                }
            });
        }

        private void InvokeAfterCefCallback(Action action)
        {
            // Let CEF's current callback return before running the action on WPF.
            Cef.UIThreadTaskFactory.StartNew(() => Dispatcher.InvokeAsync(action));
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;

            if (_browserPendingClose != null)
            {
                _browserPendingClose.IsBrowserInitializedChanged -= BrowserInitializedForClose;
                _browserPendingClose = null;
            }

            ViewModel.Browser.FrameLoadEnd -= OnBrowserFrameLoadEnd;

            Messenger.Global.Unregister(MessageTokens.CLOSE_ALL_BROWSERS, CloseBrowserHandler);
            base.OnClosed(e);
        }

        private void LoadSwf(string fileName)
        {
            try { ViewModel.Browser.ExecuteScriptAsync("loadSwf", fileName); }
            catch (Exception e) { LogHelper.LogError($"Failed to load swf file: {fileName}", e); }
        }

        private void OnBrowserFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            var browser = ViewModel?.Browser;

            if (!_isClosed && browserView.IsLoaded && browser != null && !browser.IsDisposed
                && e.Frame.IsMain
                && string.Equals(e.Url, new Uri(GlobalData.SwfPlayerPath).AbsoluteUri, StringComparison.OrdinalIgnoreCase))
            {
                LoadSwf(FileName);
            }
        }

        private void CloseBrowserHandler(object msg)
        {
            ForceCloseWindow();
        }

        public void ForceCloseWindow()
        {
            if (_isClosed || _forceClosePending)
                return;

            var browser = ViewModel?.Browser;
            if (browser != null && !browser.IsDisposed)
            {
                // Keep the native parent alive until asynchronous CEF creation finishes.
                // Pump WPF so a caller can safely shut CEF down as soon as this returns.
                _forceClosePending = true;

                var frame = new DispatcherFrame();

                EventHandler closed = (s, e) => frame.Continue = false;
                Closed += closed;

                try
                {
                    if (browser is ChromiumWebBrowser wpfBrowser && !browser.IsBrowserInitialized)
                    {
                        DeferCloseUntilBrowserInitialized(wpfBrowser);
                    }
                    else
                    {
                        InvokeAfterCefCallback(() =>
                        {
                            if (!_isClosed)
                            {
                                try
                                {
                                    browser.GetBrowser()?.CloseBrowser(true);
                                }
                                catch (ObjectDisposedException)
                                {
                                    _doClose = true;
                                    Close();
                                }
                            }
                        });
                    }

                    Dispatcher.PushFrame(frame);
                }
                finally
                {
                    Closed -= closed;
                    _forceClosePending = false;
                }
                return;
            }

            _doClose = true;
            Close();
        }

        private void DevToolsOpened(object sender, RoutedEventArgs e)
        {
            if (!_isClosed && GlobalData.Settings.EnableIntegratedDevTools)
            {
                Activate();
            }
        }

        private void DevToolsClosed(object sender, RoutedEventArgs e)
        {
            if (!_isClosed)
            {
                Activate();
            }
        }

        private void BrowserDoClose(object sender, BrowserDoCloseEventArgs e)
        {
            if (!_isClosed && !e.IsPopup && !e.IsDevTools)
            {
                _doClose = true;

                // Return from CEF's DoClose callback before destroying its parent HWND.
                InvokeAfterCefCallback(() =>
                {
                    if (!_isClosed) Close();
                });
            }
        }

        private void BrowserAfterCreated(object sender, BrowserAfterCreatedEventArgs e)
        {
            if (!_isClosed && HwndHelper.IsDevToolsWindow(e.HostHandle))
            {
                HwndHelper.SetOwnerWindow(e.HostHandle, new WindowInteropHelper(this).Handle);
            }
        }

        private void BrowserPopup(object sender, BrowserPopupEventArgs e)
        {
            if (!_isClosed)
            {
                WindowManager.ShowPopupWebPage(e.TargetUrl, e.PopupSizeInfo);
            }
        }

        private void BrowserNewPage(object sender, BrowserNewPageEventArgs e)
        {
            if (!_isClosed)
            {
                WindowManager.ShowBrowser(e.TargetUrl);
            }
        }
    }
}
