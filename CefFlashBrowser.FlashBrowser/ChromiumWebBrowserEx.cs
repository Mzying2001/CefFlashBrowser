using CefFlashBrowser.FlashBrowser.Internals;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Command;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumWebBrowserEx : ChromiumWebBrowser
    {
        public event EventHandler FullscreenModeChanged;
        public event EventHandler FaviconUrlChanged;
        public event EventHandler LoadingProgressChanged;
        public event EventHandler StatusTextChanged;
        public event EventHandler<JsContextEventArgs> JsContextCreated;
        public event EventHandler<JsContextEventArgs> JsContextReleased;


        public static readonly DependencyProperty FullscreenModeProperty;
        public static readonly DependencyProperty FaviconUrlProperty;
        public static readonly DependencyProperty LoadingProgressProperty;

        static ChromiumWebBrowserEx()
        {
            FullscreenModeProperty = DependencyProperty.Register(
                nameof(FullscreenMode), typeof(bool), typeof(ChromiumWebBrowserEx), new PropertyMetadata(false));

            FaviconUrlProperty = DependencyProperty.Register(
                nameof(FaviconUrl), typeof(string), typeof(ChromiumWebBrowserEx), new PropertyMetadata(null));

            LoadingProgressProperty = DependencyProperty.Register(
                nameof(LoadingProgress), typeof(double), typeof(ChromiumWebBrowserEx), new PropertyMetadata(0.0));
        }


        public ICommand LoadUrlCommand { get; }
        public ICommand CloseBrowserCommand { get; }
        public ICommand ShowDevToolsCommand { get; }
        public ICommand CloseDevToolsCommand { get; }

        public bool FullscreenMode
        {
            get { return (bool)GetValue(FullscreenModeProperty); }
        }

        public string FaviconUrl
        {
            get { return (string)GetValue(FaviconUrlProperty); }
        }

        public double LoadingProgress
        {
            get { return (double)GetValue(LoadingProgressProperty); }
        }


        public ChromiumWebBrowserEx()
        {
            // Apply decorator handlers
            FocusHandler = null;
            KeyboardHandler = null;
            DisplayHandler = null;
            RenderProcessMessageHandler = null;

            LoadUrlCommand = new DelegateCommand<string>(Load);
            CloseBrowserCommand = new DelegateCommand<bool>(CloseBrowser);
            ShowDevToolsCommand = new DelegateCommand(ShowDevTools);
            CloseDevToolsCommand = new DelegateCommand(CloseDevTools);
        }


        public override IFocusHandler FocusHandler
        {
            get
            {
                var handler = base.FocusHandler;
                return handler is WpfFocusHandler wpfFocusHandler ? wpfFocusHandler.InnerHandler : handler;
            }
            set
            {
                base.FocusHandler = new WpfFocusHandler(value);
            }
        }

        public override IKeyboardHandler KeyboardHandler
        {
            get
            {
                var handler = base.KeyboardHandler;
                return handler is WpfKeyboardHandler wpfKeyboardHandler ? wpfKeyboardHandler.InnerHandler : handler;
            }
            set
            {
                base.KeyboardHandler = new WpfKeyboardHandler(value);
            }
        }

        public override IDisplayHandler DisplayHandler
        {
            get
            {
                var handler = base.DisplayHandler;
                return handler is MyDisplayHandler myDisplayHandler ? myDisplayHandler.InnerHandler : handler;
            }
            set
            {
                base.DisplayHandler = new MyDisplayHandler(value);
            }
        }

        public override IRenderProcessMessageHandler RenderProcessMessageHandler
        {
            get
            {
                var handler = base.RenderProcessMessageHandler;
                return handler is MyRenderProcessMessageHandler myHandler ? myHandler.InnerHandler : handler;
            }
            set
            {
                base.RenderProcessMessageHandler = new MyRenderProcessMessageHandler(value);
            }
        }


        public void CloseBrowser(bool forceClose)
        {
            GetBrowser().CloseBrowser(forceClose);
        }

        public void ShowDevTools()
        {
            GetBrowser().ShowDevTools();
        }

        public void CloseDevTools()
        {
            GetBrowser().CloseDevTools();
        }

        protected virtual void OnFullscreenModeChanged(bool fullscreen)
        {
            SetCurrentValue(FullscreenModeProperty, fullscreen);
            FullscreenModeChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnFaviconUrlChanged(IList<string> urls)
        {
            SetCurrentValue(FaviconUrlProperty, urls != null && urls.Count > 0 ? urls[0] : null);
            FaviconUrlChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnLoadingProgressChanged(double progress)
        {
            SetCurrentValue(LoadingProgressProperty, progress);
            LoadingProgressChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnStatusMessage(StatusMessageEventArgs e)
        {
            base.OnStatusMessage(e);
            StatusTextChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnJsContextCreated(IBrowser browser, IFrame frame)
        {
            JsContextCreated?.Invoke(this, new JsContextEventArgs(browser, frame));
        }

        protected virtual void OnJsContextReleased(IBrowser browser, IFrame frame)
        {
            JsContextReleased?.Invoke(this, new JsContextEventArgs(browser, frame));
        }

        protected override void OnFrameLoadStart(FrameLoadStartEventArgs e)
        {
            base.OnFrameLoadStart(e);

            if (e.Frame.IsMain)
            {
                OnFaviconUrlChanged(null);
            }
        }


        #region Inner Classes
        class MyDisplayHandler : IDisplayHandler
        {
            public IDisplayHandler InnerHandler { get; }

            public MyDisplayHandler(IDisplayHandler innerHandler)
            {
                InnerHandler = innerHandler;
            }

            public void OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs)
            {
                InnerHandler?.OnAddressChanged(chromiumWebBrowser, addressChangedArgs);
            }

            public bool OnAutoResize(IWebBrowser chromiumWebBrowser, IBrowser browser, CefSharp.Structs.Size newSize)
            {
                return InnerHandler?.OnAutoResize(chromiumWebBrowser, browser, newSize) ?? false;
            }

            public bool OnConsoleMessage(IWebBrowser chromiumWebBrowser, ConsoleMessageEventArgs consoleMessageArgs)
            {
                return InnerHandler?.OnConsoleMessage(chromiumWebBrowser, consoleMessageArgs) ?? false;
            }

            public void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls)
            {
                if (chromiumWebBrowser is ChromiumWebBrowserEx browserEx)
                {
                    browserEx.Dispatcher.InvokeAsync(() => browserEx.OnFaviconUrlChanged(urls));
                }
                InnerHandler?.OnFaviconUrlChange(chromiumWebBrowser, browser, urls);
            }

            public void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen)
            {
                if (chromiumWebBrowser is ChromiumWebBrowserEx browserEx)
                {
                    browserEx.Dispatcher.InvokeAsync(() => browserEx.OnFullscreenModeChanged(fullscreen));
                }
                InnerHandler?.OnFullscreenModeChange(chromiumWebBrowser, browser, fullscreen);
            }

            public void OnLoadingProgressChange(IWebBrowser chromiumWebBrowser, IBrowser browser, double progress)
            {
                if (chromiumWebBrowser is ChromiumWebBrowserEx browserEx)
                {
                    browserEx.Dispatcher.InvokeAsync(() => browserEx.OnLoadingProgressChanged(progress));
                }
                InnerHandler?.OnLoadingProgressChange(chromiumWebBrowser, browser, progress);
            }

            public void OnStatusMessage(IWebBrowser chromiumWebBrowser, StatusMessageEventArgs statusMessageArgs)
            {
                InnerHandler?.OnStatusMessage(chromiumWebBrowser, statusMessageArgs);
            }

            public void OnTitleChanged(IWebBrowser chromiumWebBrowser, TitleChangedEventArgs titleChangedArgs)
            {
                InnerHandler?.OnTitleChanged(chromiumWebBrowser, titleChangedArgs);
            }

            public bool OnTooltipChanged(IWebBrowser chromiumWebBrowser, ref string text)
            {
                return InnerHandler?.OnTooltipChanged(chromiumWebBrowser, ref text) ?? false;
            }
        }

        class MyRenderProcessMessageHandler : IRenderProcessMessageHandler
        {
            public IRenderProcessMessageHandler InnerHandler { get; }

            public MyRenderProcessMessageHandler(IRenderProcessMessageHandler innerHandler)
            {
                InnerHandler = innerHandler;
            }

            public void OnContextCreated(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
            {
                if (chromiumWebBrowser is ChromiumWebBrowserEx browserEx)
                {
                    browserEx.Dispatcher.InvokeAsync(() => browserEx.OnJsContextCreated(browser, frame));
                }
                InnerHandler?.OnContextCreated(chromiumWebBrowser, browser, frame);
            }

            public void OnContextReleased(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
            {
                if (chromiumWebBrowser is ChromiumWebBrowserEx browserEx)
                {
                    browserEx.Dispatcher.InvokeAsync(() => browserEx.OnJsContextReleased(browser, frame));
                }
                InnerHandler?.OnContextReleased(chromiumWebBrowser, browser, frame);
            }

            public void OnFocusedNodeChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IDomNode node)
            {
                InnerHandler?.OnFocusedNodeChanged(chromiumWebBrowser, browser, frame, node);
            }

            public void OnUncaughtException(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, JavascriptException exception)
            {
                InnerHandler?.OnUncaughtException(chromiumWebBrowser, browser, frame, exception);
            }
        }
        #endregion
    }
}
