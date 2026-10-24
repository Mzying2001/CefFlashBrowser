using CefFlashBrowser.Data;
using CefFlashBrowser.FlashBrowser.Handlers;
using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using CefSharp;
using SimpleMvvm.Messaging;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// BrowserView.xaml 的交互逻辑
    /// </summary>
    public partial class BrowserView : UserControl
    {
        private class BrowserLifeSpanHandler : LifeSpanHandler
        {
            private readonly BrowserView view;

            public BrowserLifeSpanHandler(BrowserView view)
            {
                this.view = view;
            }

            public override bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                view.OnBrowserDoClose(chromiumWebBrowser, browser);
                return false;
            }

            public override void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                view.OnBrowserAfterCreated(chromiumWebBrowser, browser);
            }

            public override bool OnBeforePopup(
                IWebBrowser chromiumWebBrowser,
                IBrowser browser,
                IFrame frame,
                string targetUrl,
                string targetFrameName,
                WindowOpenDisposition targetDisposition,
                bool userGesture,
                IPopupFeatures popupFeatures,
                IWindowInfo windowInfo,
                IBrowserSettings browserSettings,
                ref bool noJavascriptAccess,
                out IWebBrowser newBrowser)
            {
                view.OnBrowserBeforePopup(chromiumWebBrowser, browser, frame, targetUrl, targetFrameName, targetDisposition, userGesture, popupFeatures, windowInfo, browserSettings, ref noJavascriptAccess);
                newBrowser = null;
                return true;
            }
        }

        private class BrowserMenuHandler : Browser.Handlers.ContextMenuHandler
        {
            private readonly BrowserView view;

            public BrowserMenuHandler(BrowserView view)
            {
                this.view = view;
            }

            public override bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
            {
                switch (commandId)
                {
                    case Search:
                    case OpenInNewWindow:
                    case OpenSelectedUrl:
                    case CefMenuCommand.ViewSource:
                        {
                            view.Dispatcher.Invoke(() =>
                            {
                                view.ViewModel.ExitFullScreen();
                                view.OnFullScreenChanged(false);
                            });
                            break;
                        }
                }
                return base.OnContextMenuCommand(chromiumWebBrowser, browser, frame, parameters, commandId, eventFlags);
            }
        }


        public static readonly RoutedEvent DevToolsOpenedEvent = EventManager.RegisterRoutedEvent(
            nameof(DevToolsOpened), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BrowserView));

        public static readonly RoutedEvent DevToolsClosedEvent = EventManager.RegisterRoutedEvent(
            nameof(DevToolsClosed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(BrowserView));

        public static readonly RoutedEvent FullscreenChangedEvent = EventManager.RegisterRoutedEvent(
            nameof(FullscreenChanged), RoutingStrategy.Bubble, typeof(FullscreenChangedEventHandler), typeof(BrowserView));

        public static readonly RoutedEvent BrowserDoCloseEvent = EventManager.RegisterRoutedEvent(
            nameof(BrowserDoClose), RoutingStrategy.Bubble, typeof(BrowserDoCloseEventHandler), typeof(BrowserView));

        public static readonly RoutedEvent BrowserAfterCreatedEvent = EventManager.RegisterRoutedEvent(
            nameof(BrowserAfterCreated), RoutingStrategy.Bubble, typeof(BrowserAfterCreatedEventHandler), typeof(BrowserView));

        public static readonly RoutedEvent BrowserPopupEvent = EventManager.RegisterRoutedEvent(
            nameof(BrowserPopup), RoutingStrategy.Bubble, typeof(BrowserPopupEventHandler), typeof(BrowserView));

        public static readonly RoutedEvent BrowserNewPageEvent = EventManager.RegisterRoutedEvent(
            nameof(BrowserNewPage), RoutingStrategy.Bubble, typeof(BrowserNewPageEventHandler), typeof(BrowserView));


        public event RoutedEventHandler DevToolsOpened
        {
            add => AddHandler(DevToolsOpenedEvent, value);
            remove => RemoveHandler(DevToolsOpenedEvent, value);
        }

        public event RoutedEventHandler DevToolsClosed
        {
            add => AddHandler(DevToolsClosedEvent, value);
            remove => RemoveHandler(DevToolsClosedEvent, value);
        }

        public event FullscreenChangedEventHandler FullscreenChanged
        {
            add => AddHandler(FullscreenChangedEvent, value);
            remove => RemoveHandler(FullscreenChangedEvent, value);
        }

        public event BrowserDoCloseEventHandler BrowserDoClose
        {
            add => AddHandler(BrowserDoCloseEvent, value);
            remove => RemoveHandler(BrowserDoCloseEvent, value);
        }

        public event BrowserAfterCreatedEventHandler BrowserAfterCreated
        {
            add => AddHandler(BrowserAfterCreatedEvent, value);
            remove => RemoveHandler(BrowserAfterCreatedEvent, value);
        }

        public event BrowserPopupEventHandler BrowserPopup
        {
            add => AddHandler(BrowserPopupEvent, value);
            remove => RemoveHandler(BrowserPopupEvent, value);
        }

        public event BrowserNewPageEventHandler BrowserNewPage
        {
            add => AddHandler(BrowserNewPageEvent, value);
            remove => RemoveHandler(BrowserNewPageEvent, value);
        }

        public BrowserViewModel ViewModel
        {
            get => DataContext as BrowserViewModel;
            set => DataContext = value;
        }

        public BrowserView()
        {
            InitializeComponent();

            browser.JsDialogHandler = new Browser.Handlers.JsDialogHandler();
            browser.DownloadHandler = new Browser.Handlers.DownloadHandler();
            browser.LifeSpanHandler = new BrowserLifeSpanHandler(this);
            browser.MenuHandler = new BrowserMenuHandler(this);

            Messenger.Global.Register(MessageTokens.DEVTOOLS_OPENED, DevToolsOpenedHandler);
            Messenger.Global.Register(MessageTokens.DEVTOOLS_CLOSED, DevToolsClosedHandler);
        }

        private void UnregisterMessageHandlers()
        {
            Messenger.Global.Unregister(MessageTokens.DEVTOOLS_OPENED, DevToolsOpenedHandler);
            Messenger.Global.Unregister(MessageTokens.DEVTOOLS_CLOSED, DevToolsClosedHandler);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            Dispatcher.InvokeAsync(() =>
            {
                if (ViewModel != null)
                {
                    ViewModel.Browser = browser;
                }
            });
        }

        private void DevToolsOpenedHandler(object msg)
        {
            if (msg == browser)
            {
                OnDevToolsOpened();
            }
        }

        private void DevToolsClosedHandler(object msg)
        {
            if (msg == browser)
            {
                OnDevToolsClosed();
            }
        }

        protected virtual void OnDevToolsOpened()
        {
            var e = new RoutedEventArgs(DevToolsOpenedEvent, this);
            RaiseEvent(e);

            if (!e.Handled && GlobalData.Settings.EnableIntegratedDevTools)
            {
                Keyboard.Focus(browser);
                devtoolsColumn.Width = new GridLength(GlobalData.Settings.IntegratedDevToolsWidth);
            }
        }

        protected virtual void OnDevToolsClosed()
        {
            var e = new RoutedEventArgs(DevToolsClosedEvent, this);
            RaiseEvent(e);

            if (!e.Handled)
            {
                Keyboard.Focus(browser);
                GlobalData.Settings.IntegratedDevToolsWidth = devtoolsColumn.Width.Value;
                devtoolsColumn.Width = new GridLength(0);
            }
        }

        private void BrowserFullscreenModeChanged(object sender, EventArgs e)
        {
            OnFullScreenChanged(browser.FullscreenMode);
        }

        protected virtual void OnFullScreenChanged(bool fullScreen)
        {
            RaiseEvent(new FullscreenChangedEventArgs(FullscreenChangedEvent, this, fullScreen));
        }

        protected virtual void OnBrowserDoClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            var isPopup = browser.IsPopup;
            var hostHandle = browser.GetHost().GetWindowHandle();
            var isDevTools = HwndHelper.IsDevToolsWindow(hostHandle);

            Dispatcher.Invoke(() =>
            {
                var e = new BrowserDoCloseEventArgs(BrowserDoCloseEvent, this, chromiumWebBrowser, browser, isPopup, hostHandle);
                RaiseEvent(e);

                if (!e.Handled && isDevTools)
                {
                    ViewModel.OnDevToolsClosed();
                }

                if (!isPopup && !isDevTools)
                {
                    UnregisterMessageHandlers();

                    if (devtoolsContainer.ContentHandle != IntPtr.Zero)
                        GlobalData.Settings.IntegratedDevToolsWidth = devtoolsColumn.Width.Value;
                }
            });
        }

        protected virtual void OnBrowserAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
        {
            var hostHandle = browser.GetHost().GetWindowHandle();
            var isDevTools = HwndHelper.IsDevToolsWindow(hostHandle);

            if (isDevTools)
            {
                HwndHelper.SetDevToolsFlag(hostHandle);
            }

            Dispatcher.Invoke(() =>
            {
                var e = new BrowserAfterCreatedEventArgs(BrowserAfterCreatedEvent, this, chromiumWebBrowser, browser, hostHandle);
                RaiseEvent(e);

                if (!e.Handled && isDevTools)
                {
                    ViewModel.OnDevToolsOpened(hostHandle);
                }
            });
        }

        protected virtual void OnBrowserBeforePopup(
            IWebBrowser chromiumWebBrowser,
            IBrowser browser,
            IFrame frame,
            string targetUrl,
            string targetFrameName,
            WindowOpenDisposition targetDisposition,
            bool userGesture,
            IPopupFeatures popupFeatures,
            IWindowInfo windowInfo,
            IBrowserSettings browserSettings,
            ref bool noJavascriptAccess)
        {
            var isPopup = targetDisposition == WindowOpenDisposition.NewPopup;

            // Extract popupFeatures properties in synchronous context,
            // as CEF disposes the object after OnBeforePopup returns.
            WindowSizeInfo popupSizeInfo = null;

            if (isPopup && popupFeatures != null)
            {
                popupSizeInfo = new WindowSizeInfo
                {
                    Left = popupFeatures.XSet != 0 ? popupFeatures.X : double.NaN,
                    Top = popupFeatures.YSet != 0 ? popupFeatures.Y : double.NaN,
                    Width = popupFeatures.WidthSet != 0 ? popupFeatures.Width : double.NaN,
                    Height = popupFeatures.HeightSet != 0 ? popupFeatures.Height : double.NaN,
                };
            }

            Dispatcher.InvokeAsync(() =>
            {
                if (isPopup)
                {
                    var e = new BrowserPopupEventArgs(BrowserPopupEvent, this, chromiumWebBrowser, browser, targetUrl, popupSizeInfo);
                    RaiseEvent(e);
                }
                else
                {
                    var e = new BrowserNewPageEventArgs(BrowserNewPageEvent, this, chromiumWebBrowser, browser, targetUrl);
                    RaiseEvent(e);
                }
            });
        }

        private void BrowserTitleChanged(object sender, TitleChangedEventArgs e)
        {
            ViewModel.Title = e.Title;
        }
    }

    public class FullscreenChangedEventArgs : RoutedEventArgs
    {
        public bool Fullscreen { get; }

        public FullscreenChangedEventArgs(RoutedEvent routedEvent, object source, bool fullscreen)
            : base(routedEvent, source)
        {
            Fullscreen = fullscreen;
        }
    }

    public delegate void FullscreenChangedEventHandler(object sender, FullscreenChangedEventArgs e);

    public class BrowserDoCloseEventArgs : RoutedEventArgs
    {
        public IWebBrowser ChromiumWebBrowser { get; }
        public IBrowser Browser { get; }
        public bool IsPopup { get; }
        public IntPtr HostHandle { get; }

        public BrowserDoCloseEventArgs(RoutedEvent routedEvent, object source, IWebBrowser chromiumWebBrowser, IBrowser browser, bool isPopup, IntPtr hostHandle)
            : base(routedEvent, source)
        {
            ChromiumWebBrowser = chromiumWebBrowser;
            Browser = browser;
            IsPopup = isPopup;
            HostHandle = hostHandle;
        }
    }

    public delegate void BrowserDoCloseEventHandler(object sender, BrowserDoCloseEventArgs e);

    public class BrowserAfterCreatedEventArgs : RoutedEventArgs
    {
        public IWebBrowser ChromiumWebBrowser { get; }
        public IBrowser Browser { get; }
        public IntPtr HostHandle { get; }

        public BrowserAfterCreatedEventArgs(RoutedEvent routedEvent, object source, IWebBrowser chromiumWebBrowser, IBrowser browser, IntPtr hostHandle)
            : base(routedEvent, source)
        {
            ChromiumWebBrowser = chromiumWebBrowser;
            Browser = browser;
            HostHandle = hostHandle;
        }
    }

    public delegate void BrowserAfterCreatedEventHandler(object sender, BrowserAfterCreatedEventArgs e);

    public class BrowserPopupEventArgs : RoutedEventArgs
    {
        public IWebBrowser ChromiumWebBrowser { get; }
        public IBrowser Browser { get; }
        public string TargetUrl { get; }
        public WindowSizeInfo PopupSizeInfo { get; }

        public BrowserPopupEventArgs(RoutedEvent routedEvent, object source, IWebBrowser chromiumWebBrowser, IBrowser browser, string targetUrl, WindowSizeInfo popupSizeInfo)
            : base(routedEvent, source)
        {
            ChromiumWebBrowser = chromiumWebBrowser;
            Browser = browser;
            TargetUrl = targetUrl;
            PopupSizeInfo = popupSizeInfo;
        }
    }

    public delegate void BrowserPopupEventHandler(object sender, BrowserPopupEventArgs e);

    public class BrowserNewPageEventArgs : RoutedEventArgs
    {
        public IWebBrowser ChromiumWebBrowser { get; }
        public IBrowser Browser { get; }
        public string TargetUrl { get; }

        public BrowserNewPageEventArgs(RoutedEvent routedEvent, object source, IWebBrowser chromiumWebBrowser, IBrowser browser, string targetUrl)
            : base(routedEvent, source)
        {
            ChromiumWebBrowser = chromiumWebBrowser;
            Browser = browser;
            TargetUrl = targetUrl;
        }
    }

    public delegate void BrowserNewPageEventHandler(object sender, BrowserNewPageEventArgs e);
}
