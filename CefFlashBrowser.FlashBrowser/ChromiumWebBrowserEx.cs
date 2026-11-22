using CefFlashBrowser.FlashBrowser.Internals;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Command;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser
{
    public class ChromiumWebBrowserEx : ChromiumWebBrowser
    {
        private readonly object _navigationLock = new object();
        private long _navigationVersion;
        private string _pageUrl;

        public event EventHandler FullscreenModeChanged;
        public event EventHandler FaviconUrlChanged;
        public event EventHandler<FaviconDiscoveredEventArgs> FaviconDiscovered;
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


        public ChromiumWebBrowserEx() : this(false)
        {
        }

        public ChromiumWebBrowserEx(bool deferNativeCreation) : base(deferNativeCreation)
        {
            // Apply decorator handlers
            FocusHandler = null;
            KeyboardHandler = null;
            DisplayHandler = null;
            RenderProcessMessageHandler = null;
            ResourceRequestHandlerFactory = null;

            LoadUrlCommand = new DelegateCommand<string>(Load);
            CloseBrowserCommand = new DelegateCommand<bool>(CloseBrowser);
            ShowDevToolsCommand = new DelegateCommand(ShowDevTools);
            CloseDevToolsCommand = new DelegateCommand(CloseDevTools);
        }


        public override IFocusHandler FocusHandler
        {
            get => (base.FocusHandler as WpfFocusHandler)?.InnerHandler;
            set => base.FocusHandler = new WpfFocusHandler(value);
        }

        public override IKeyboardHandler KeyboardHandler
        {
            get => (base.KeyboardHandler as WpfKeyboardHandler)?.InnerHandler;
            set => base.KeyboardHandler = new WpfKeyboardHandler(value);
        }

        public override IDisplayHandler DisplayHandler
        {
            get => (base.DisplayHandler as DecoratedDisplayHandler)?.InnerHandler;
            set => base.DisplayHandler = new DecoratedDisplayHandler(value);
        }

        public override IRenderProcessMessageHandler RenderProcessMessageHandler
        {
            get => (base.RenderProcessMessageHandler as DecoratedRenderProcessMessageHandler)?.InnerHandler;
            set => base.RenderProcessMessageHandler = new DecoratedRenderProcessMessageHandler(value);
        }

        public override IRequestHandler RequestHandler
        {
            get => (base.RequestHandler as DecoratedRequestHandler)?.InnerHandler;
            set => base.RequestHandler = value == null ? null : new DecoratedRequestHandler(this, value);
        }

        public override IResourceRequestHandlerFactory ResourceRequestHandlerFactory
        {
            get => (base.ResourceRequestHandlerFactory as DecoratedResourceRequestHandlerFactory)?.InnerFactory;
            set => base.ResourceRequestHandlerFactory = new DecoratedResourceRequestHandlerFactory(this, value);
        }


        // Capture any state needed by the observer before the factory can dispose the request.
        // The observer receives the URL and response MIME type, or null before the request is sent.
        protected virtual Action<string, string> CreateResourceRequestObserver(
            IBrowser browser, IFrame frame, IRequest request)
        {
            return null;
        }

        protected virtual Action<string, string> CreateResourceRequestObserver(
            IBrowser browser, IFrame frame, IRequest request, string requestInitiator)
        {
            return CreateResourceRequestObserver(browser, frame, request);
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

        public Task<bool> GetAudioMutedAsync()
        {
            return Cef.UIThreadTaskFactory.StartNew(() =>
            {
                if (IsDisposed || !IsBrowserInitialized)
                    return false;

                try
                {
                    var native = GetBrowser();

                    if (native == null || native.IsDisposed)
                        return false;

                    var host = native.GetHost();

                    if (host == null || host.IsDisposed)
                        return false;

                    return host.IsAudioMuted;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            });
        }

        public Task SetAudioMutedAsync(bool mute)
        {
            return Cef.UIThreadTaskFactory.StartNew(() =>
            {
                if (IsDisposed || !IsBrowserInitialized)
                    return;

                try
                {
                    var browser = GetBrowser();

                    if (browser == null || browser.IsDisposed)
                        return;

                    var host = browser.GetHost();

                    if (host != null && !host.IsDisposed)
                        host.SetAudioMuted(mute);
                }
                catch (ObjectDisposedException)
                {
                    // The browser may finish closing while the audio update is queued.
                }
            });
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
            if (e.Frame.IsMain)
            {
                UpdateNavigation(e.Frame.Url);
            }

            base.OnFrameLoadStart(e);

            if (!IsDisposed && e.Frame.IsMain)
                OnFaviconUrlChanged(null);
        }

        private void UpdateNavigation(string pageUrl)
        {
            lock (_navigationLock)
            {
                _navigationVersion++;
                _pageUrl = pageUrl;
            }
        }

        private void CaptureFavicon(IBrowser browser, IList<string> urls)
        {
            string iconUrl = urls != null && urls.Count > 0 ? urls[0] : null;
            long version;

            lock (_navigationLock)
                version = _navigationVersion;

            string pageUrl;
            try
            {
                if (IsDisposed || browser == null)
                    return;

                using (var frame = browser.MainFrame)
                    pageUrl = frame?.Url;
            }
            catch (ObjectDisposedException) { return; }

            lock (_navigationLock)
            {
                if (version != _navigationVersion || pageUrl != _pageUrl)
                    return;
            }

            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                lock (_navigationLock)
                {
                    if (IsDisposed || version != _navigationVersion || pageUrl != _pageUrl)
                        return;
                }

                OnFaviconUrlChanged(iconUrl == null ? null : new[] { iconUrl });

                lock (_navigationLock)
                {
                    if (IsDisposed || version != _navigationVersion || pageUrl != _pageUrl)
                        return;
                }

                if (!string.IsNullOrWhiteSpace(iconUrl))
                    FaviconDiscovered?.Invoke(this, new FaviconDiscoveredEventArgs(pageUrl, iconUrl));
            });
        }

        protected override void Dispose(bool disposing)
        {
            UpdateNavigation(null);
            base.Dispose(disposing);
        }


        #region Inner Classes
        private sealed class DecoratedRequestHandler : IRequestHandler
        {
            private readonly ChromiumWebBrowserEx _owner;
            public IRequestHandler InnerHandler { get; }

            public DecoratedRequestHandler(ChromiumWebBrowserEx owner, IRequestHandler innerHandler)
            {
                _owner = owner;
                InnerHandler = innerHandler;
            }

            public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser control, IBrowser browser, IFrame frame,
                IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
            {
                var observe = _owner.CreateResourceRequestObserver(browser, frame, request, requestInitiator);
                var handler = InnerHandler.GetResourceRequestHandler(control, browser, frame, request, isNavigation,
                    isDownload, requestInitiator, ref disableDefaultHandling);

                // A null return lets CefSharp look up the registered factory next.
                return handler != null && observe != null ? new ObservedResourceRequestHandler(handler, observe) : handler;
            }

            public bool OnBeforeBrowse(IWebBrowser control, IBrowser browser, IFrame frame, IRequest request, bool userGesture, bool isRedirect)
                => InnerHandler.OnBeforeBrowse(control, browser, frame, request, userGesture, isRedirect);

            public bool OnOpenUrlFromTab(IWebBrowser control, IBrowser browser, IFrame frame,
                string targetUrl, WindowOpenDisposition targetDisposition, bool userGesture)
                => InnerHandler.OnOpenUrlFromTab(control, browser, frame, targetUrl, targetDisposition, userGesture);

            public bool GetAuthCredentials(IWebBrowser control, IBrowser browser, string originUrl, bool isProxy,
                string host, int port, string realm, string scheme, IAuthCallback callback)
                => InnerHandler.GetAuthCredentials(control, browser, originUrl, isProxy, host, port, realm, scheme, callback);

            public bool OnQuotaRequest(IWebBrowser control, IBrowser browser, string originUrl, long newSize, IRequestCallback callback)
                => InnerHandler.OnQuotaRequest(control, browser, originUrl, newSize, callback);

            public bool OnCertificateError(IWebBrowser control, IBrowser browser, CefErrorCode errorCode, string requestUrl,
                ISslInfo sslInfo, IRequestCallback callback)
                => InnerHandler.OnCertificateError(control, browser, errorCode, requestUrl, sslInfo, callback);

            public bool OnSelectClientCertificate(IWebBrowser control, IBrowser browser, bool isProxy, string host, int port,
                X509Certificate2Collection certificates, ISelectClientCertificateCallback callback)
                => InnerHandler.OnSelectClientCertificate(control, browser, isProxy, host, port, certificates, callback);

            public void OnPluginCrashed(IWebBrowser control, IBrowser browser, string pluginPath)
                => InnerHandler.OnPluginCrashed(control, browser, pluginPath);

            public void OnRenderViewReady(IWebBrowser control, IBrowser browser)
                => InnerHandler.OnRenderViewReady(control, browser);

            public void OnRenderProcessTerminated(IWebBrowser control, IBrowser browser, CefTerminationStatus status)
                => InnerHandler.OnRenderProcessTerminated(control, browser, status);

            public void OnDocumentAvailableInMainFrame(IWebBrowser control, IBrowser browser)
                => InnerHandler.OnDocumentAvailableInMainFrame(control, browser);
        }

        private sealed class DecoratedResourceRequestHandlerFactory : IResourceRequestHandlerFactory
        {
            private readonly ChromiumWebBrowserEx _owner;
            public IResourceRequestHandlerFactory InnerFactory { get; }

            // Derived controls can decorate requests that have no registered handler.
            public bool HasHandlers => true;

            public DecoratedResourceRequestHandlerFactory(ChromiumWebBrowserEx owner, IResourceRequestHandlerFactory innerFactory)
            {
                _owner = owner;
                InnerFactory = innerFactory;
            }

            public IResourceRequestHandler GetResourceRequestHandler(IWebBrowser control, IBrowser browser, IFrame frame,
                IRequest request, bool isNavigation, bool isDownload, string requestInitiator, ref bool disableDefaultHandling)
            {
                // CefSharp's factory disposes the request before returning.
                var observe = _owner.CreateResourceRequestObserver(browser, frame, request, requestInitiator);
                IResourceRequestHandler handler = null;

                if (InnerFactory?.HasHandlers == true)
                {
                    handler = InnerFactory.GetResourceRequestHandler(control, browser, frame, request, isNavigation,
                        isDownload, requestInitiator, ref disableDefaultHandling);
                }

                if (handler == null && disableDefaultHandling)
                    return null;

                return observe != null ? new ObservedResourceRequestHandler(handler, observe) : handler;
            }
        }

        private sealed class ObservedResourceRequestHandler : IResourceRequestHandler
        {
            private readonly IResourceRequestHandler _innerHandler;
            private readonly Action<string, string> _observe;

            public ObservedResourceRequestHandler(IResourceRequestHandler innerHandler, Action<string, string> observe)
            {
                _innerHandler = innerHandler;
                _observe = observe;
            }

            public CefReturnValue OnBeforeResourceLoad(IWebBrowser control, IBrowser browser, IFrame frame,
                IRequest request, IRequestCallback callback)
            {
                var result = _innerHandler?.OnBeforeResourceLoad(control, browser, frame, request, callback) ?? CefReturnValue.Continue;
                if (result == CefReturnValue.Continue)
                    _observe(request.Url, null);

                return result;
            }

            public bool OnResourceResponse(IWebBrowser control, IBrowser browser, IFrame frame, IRequest request, IResponse response)
            {
                var handled = _innerHandler?.OnResourceResponse(control, browser, frame, request, response) ?? false;
                if (!handled)
                    _observe(request.Url, response.MimeType);

                return handled;
            }

            public ICookieAccessFilter GetCookieAccessFilter(IWebBrowser control, IBrowser browser, IFrame frame, IRequest request)
                => _innerHandler?.GetCookieAccessFilter(control, browser, frame, request);

            public IResourceHandler GetResourceHandler(IWebBrowser control, IBrowser browser, IFrame frame, IRequest request)
                => _innerHandler?.GetResourceHandler(control, browser, frame, request);

            public void OnResourceRedirect(IWebBrowser control, IBrowser browser, IFrame frame, IRequest request, IResponse response, ref string newUrl)
                => _innerHandler?.OnResourceRedirect(control, browser, frame, request, response, ref newUrl);

            public IResponseFilter GetResourceResponseFilter(IWebBrowser control, IBrowser browser, IFrame frame, IRequest request, IResponse response)
                => _innerHandler?.GetResourceResponseFilter(control, browser, frame, request, response);

            public void OnResourceLoadComplete(IWebBrowser control, IBrowser browser, IFrame frame, IRequest request, IResponse response,
                UrlRequestStatus status, long receivedContentLength)
                => _innerHandler?.OnResourceLoadComplete(control, browser, frame, request, response, status, receivedContentLength);

            public bool OnProtocolExecution(IWebBrowser control, IBrowser browser, IFrame frame, IRequest request)
                => _innerHandler?.OnProtocolExecution(control, browser, frame, request) ?? false;

            public void Dispose() => _innerHandler?.Dispose();
        }

        class DecoratedDisplayHandler : IDisplayHandler
        {
            public IDisplayHandler InnerHandler { get; }

            public DecoratedDisplayHandler(IDisplayHandler innerHandler)
            {
                InnerHandler = innerHandler;
            }

            public void OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs)
            {
                if (chromiumWebBrowser is ChromiumWebBrowserEx browserEx)
                {
                    browserEx.UpdateNavigation(addressChangedArgs.Address);
                }
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
                    browserEx.CaptureFavicon(browser, urls);
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

        class DecoratedRenderProcessMessageHandler : IRenderProcessMessageHandler
        {
            public IRenderProcessMessageHandler InnerHandler { get; }

            public DecoratedRenderProcessMessageHandler(IRenderProcessMessageHandler innerHandler)
            {
                InnerHandler = innerHandler;
            }

            public void OnContextCreated(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
            {
                // Fix: Intercept GET form submissions targeting _blank to preserve
                // query parameters that would otherwise be lost in OnBeforePopup.
                frame.ExecuteJavaScriptAsync(@"
                    (function() {
                        document.addEventListener('submit', function(e) {
                            if (e.defaultPrevented) return;
                            var form = e.target;
                            var target = form.target;
                            if (!target) {
                                var b = document.querySelector('base[target]');
                                if (b) target = b.target;
                            }
                            if (!target || target.toLowerCase() !== '_blank') return;
                            if (form.method && form.method.toLowerCase() === 'post') return;
                            e.preventDefault();
                            var fd = new FormData(form);
                            var params = new URLSearchParams(fd).toString();
                            var url = form.action;
                            if (params) url += (url.indexOf('?') >= 0 ? '&' : '?') + params;
                            window.open(url, '_blank');
                        }, false);
                    })();
                ");

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
