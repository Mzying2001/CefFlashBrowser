using CefFlashBrowser.Data;
using CefFlashBrowser.Services;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using CefSharp;
using SimpleMvvm.Ioc;
using SimpleMvvm.Messaging;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// PopupWebPage.xaml 的交互逻辑
    /// </summary>
    public partial class PopupWebPage : Window
    {
        private bool _doClose = false;
        private bool _isClosed = false;
        private Task _windowCloseTask;
        private bool _forceClosePending;

        internal bool IsClosing => _doClose || _forceClosePending || _windowCloseTask != null && !_windowCloseTask.IsCompleted;

        private BrowserViewModel ViewModel => browserView?.ViewModel;

        public string Address
        {
            get => (string)GetValue(AddressProperty);
            set => SetValue(AddressProperty, value);
        }

        public static readonly DependencyProperty AddressProperty =
            DependencyProperty.Register(nameof(Address), typeof(string), typeof(PopupWebPage), new PropertyMetadata(null, OnAddressPropertyChanged));

        private static void OnAddressPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var window = (PopupWebPage)d;

            if (!window._isClosed && window.ViewModel != null)
                window.ViewModel.Address = e.NewValue as string;
        }

        public PopupWebPage()
        {
            InitializeComponent();

            browserView.ViewModel = SimpleIoc.Global.GetInstance<BrowserViewModel>(Address ?? "about:blank");

            Messenger.Global.Register(MessageTokens.CLOSE_ALL_BROWSERS, CloseBrowserHandler);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            var browser = ViewModel?.Browser;
            if (_doClose || browser == null || browser.IsDisposed)
            {
                // The browser has completed accepted-close cleanup.
            }
            else
            {
                e.Cancel = true;

                if (_windowCloseTask == null || _windowCloseTask.IsCompleted)
                    _windowCloseTask = CloseBrowserAsync(false);
            }

            base.OnClosing(e);
        }

        private async Task CloseBrowserAsync(bool force)
        {
            // Leave WPF's Closing event before requesting or completing a close.
            await Task.Yield();

            if (await browserView.RequestCloseAsync(force) && !_isClosed)
            {
                _doClose = true;
                Close();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _isClosed = true;
            ViewModel?.MarkClosed();

            Messenger.Global.Unregister(MessageTokens.CLOSE_ALL_BROWSERS, CloseBrowserHandler);
            base.OnClosed(e);
        }

        private void CloseBrowserHandler(object msg)
        {
            ForceCloseWindow();
        }

        public void ForceCloseWindow()
        {
            if (_isClosed || _forceClosePending)
                return;

            _forceClosePending = true;

            try
            {
                var closing = CloseBrowserAsync(true);

                if (!closing.IsCompleted)
                {
                    var frame = new DispatcherFrame();

                    closing.GetAwaiter().OnCompleted(() => Dispatcher.InvokeAsync(() => frame.Continue = false));
                    Dispatcher.PushFrame(frame);
                }

                closing.GetAwaiter().GetResult();
            }
            finally
            {
                _forceClosePending = false;
            }
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

        private async void BrowserDoClose(object sender, BrowserDoCloseEventArgs e)
        {
            if (!_isClosed && !e.IsPopup && !e.IsDevTools)
            {
                // Suppress CEF's parent WM_CLOSE until the shared view has drained
                // its host, accepted work and DevTools with native context intact.
                e.Handled = true;
                await browserView.CompleteCloseAsync();

                if (!_isClosed)
                {
                    _doClose = true;
                    Close();
                }
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
