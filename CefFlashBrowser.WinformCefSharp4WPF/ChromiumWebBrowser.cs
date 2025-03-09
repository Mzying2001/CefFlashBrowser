using CefFlashBrowser.WinformCefSharp4WPF.Internals;
using CefSharp;
using CefSharp.Internals;
using SimpleMvvm.Command;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace CefFlashBrowser.WinformCefSharp4WPF
{
    public class ChromiumWebBrowser : HwndHost, IWebBrowserInternal, IWebBrowser, IWpfWebBrowser, IDisposable
    {
        /// <summary>
        /// Flag used to determine whether the address change is notified by the base browser
        /// </summary>
        private bool onNotifyAddressChanged = false;
        private readonly object onNotifyAddressChangedLock = new object();

        /// <summary>
        /// The base browser
        /// </summary>
        private readonly CefSharp.WinForms.ChromiumWebBrowser browser;



        private const double ZOOMLEVELMAX = 7d;
        private const double ZOOMLEVELMIN = -7d;
        private const double ZOOMLEVELDEFAULT = 0d;
        private const double ZOOMLEVELINCREMENTDEFAULT = 0.1d;



        public event EventHandler<JavascriptMessageReceivedEventArgs> JavascriptMessageReceived;
        public event EventHandler<ConsoleMessageEventArgs> ConsoleMessage;
        public event EventHandler<StatusMessageEventArgs> StatusMessage;
        public event EventHandler<FrameLoadStartEventArgs> FrameLoadStart;
        public event EventHandler<FrameLoadEndEventArgs> FrameLoadEnd;
        public event EventHandler<LoadErrorEventArgs> LoadError;
        public event EventHandler<LoadingStateChangedEventArgs> LoadingStateChanged;

        public event EventHandler<AddressChangedEventArgs> AddressChanged;
        public event EventHandler<TitleChangedEventArgs> TitleChanged;
        public event EventHandler IsBrowserInitializedChanged;



        public IJavascriptObjectRepository JavascriptObjectRepository
        {
            get => browser.JavascriptObjectRepository;
        }

        private IAudioHandler audioHandler;
        public virtual IAudioHandler AudioHandler
        {
            get => audioHandler;
            set
            {
                audioHandler = value;
                browser.AudioHandler = value == null ? null : new AudioHandlerWrapper(audioHandler, this);
            }
        }

        private IDialogHandler dialogHandler = null;
        public virtual IDialogHandler DialogHandler
        {
            get => dialogHandler;
            set
            {
                dialogHandler = value;
                browser.DialogHandler = value == null ? null : new DialogHandlerWrapper(dialogHandler, this);
            }
        }

        private IRequestHandler requestHandler = null;
        public virtual IRequestHandler RequestHandler
        {
            get => requestHandler;
            set
            {
                requestHandler = value;
                browser.RequestHandler = value == null ? null : new RequestHandlerWrapper(requestHandler, this);
            }
        }

        private IDisplayHandler displayHandler = null;
        public virtual IDisplayHandler DisplayHandler
        {
            get => displayHandler;
            set
            {
                displayHandler = value;
                browser.DisplayHandler = value == null ? null : new DisplayHandlerWrapper(displayHandler, this);
            }
        }

        private ILoadHandler loadHandler = null;
        public virtual ILoadHandler LoadHandler
        {
            get => loadHandler;
            set
            {
                loadHandler = value;
                browser.LoadHandler = value == null ? null : new LoadHandlerWrapper(loadHandler, this);
            }
        }

        private ILifeSpanHandler lifeSpanHandler = null;
        public virtual ILifeSpanHandler LifeSpanHandler
        {
            get => lifeSpanHandler;
            set
            {
                lifeSpanHandler = value;
                browser.LifeSpanHandler = value == null ? null : new LifeSpanHandlerWrapper(lifeSpanHandler, this);
            }
        }

        private IKeyboardHandler keyboardHandler = null;
        public virtual IKeyboardHandler KeyboardHandler
        {
            get => keyboardHandler;
            set
            {
                keyboardHandler = value;
                browser.KeyboardHandler = value == null ? null : new KeyboardHandlerWrapper(keyboardHandler, this);
            }
        }

        private IJsDialogHandler jsDialogHandler = null;
        public virtual IJsDialogHandler JsDialogHandler
        {
            get => jsDialogHandler;
            set
            {
                jsDialogHandler = value;
                browser.JsDialogHandler = value == null ? null : new JsDialogHandlerWrapper(jsDialogHandler, this);
            }
        }

        private IDragHandler dragHandler = null;
        public virtual IDragHandler DragHandler
        {
            get => dragHandler;
            set
            {
                dragHandler = value;
                browser.DragHandler = value == null ? null : new DragHandlerWrapper(dragHandler, this);
            }
        }

        private IDownloadHandler downloadHandler = null;
        public virtual IDownloadHandler DownloadHandler
        {
            get => downloadHandler;
            set
            {
                downloadHandler = value;
                browser.DownloadHandler = value == null ? null : new DownloadHandlerWrapper(downloadHandler, this);
            }
        }

        private IContextMenuHandler menuHandler = null;
        public virtual IContextMenuHandler MenuHandler
        {
            get => menuHandler;
            set
            {
                menuHandler = value;
                browser.MenuHandler = value == null ? null : new ContextMenuHandlerWrapper(menuHandler, this);
            }
        }

        private IFocusHandler focusHandler = null;
        public virtual IFocusHandler FocusHandler
        {
            get => focusHandler;
            set
            {
                focusHandler = value;
                browser.FocusHandler = value == null ? null : new FocusHandlerWrapper(focusHandler, this);
            }
        }

        private IResourceRequestHandlerFactory resourceRequestHandlerFactory = null;
        public virtual IResourceRequestHandlerFactory ResourceRequestHandlerFactory
        {
            get => resourceRequestHandlerFactory;
            set
            {
                resourceRequestHandlerFactory = value;
                browser.ResourceRequestHandlerFactory = value == null ? null : new ResourceRequestHandlerFactoryWrapper(resourceRequestHandlerFactory, this);
            }
        }

        private IRenderProcessMessageHandler renderProcessMessageHandler = null;
        public virtual IRenderProcessMessageHandler RenderProcessMessageHandler
        {
            get => renderProcessMessageHandler;
            set
            {
                renderProcessMessageHandler = value;
                browser.RenderProcessMessageHandler = value == null ? null : new RenderProcessMessageHandlerWrapper(renderProcessMessageHandler, this);
            }
        }

        private IFindHandler findHandler = null;
        public virtual IFindHandler FindHandler
        {
            get => findHandler;
            set
            {
                findHandler = value;
                browser.FindHandler = value == null ? null : new FindHandlerWrapper(findHandler, this);
            }
        }

        public string TooltipText
        {
            get => browser.TooltipText;
        }

        public bool CanExecuteJavascriptInMainFrame
        {
            get => browser.CanExecuteJavascriptInMainFrame;
        }

        public IRequestContext RequestContext
        {
            get => browser.RequestContext;
        }

        public bool IsBrowserInitialized
        {
            get => (bool)GetValue(IsBrowserInitializedProperty);
        }

        public bool IsDisposed
        {
            get => browser.IsDisposed;
        }

        public bool IsLoading
        {
            get => (bool)GetValue(IsLoadingProperty);
        }

        public bool CanGoBack
        {
            get => (bool)GetValue(CanGoBackProperty);
        }

        public bool CanGoForward
        {
            get => (bool)GetValue(CanGoForwardProperty);
        }

        public string Address
        {
            get => (string)GetValue(AddressProperty);
            set => SetValue(AddressProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
        }

        public string StatusText
        {
            get => (string)GetValue(StatusTextProperty);
        }

        public double ZoomLevel
        {
            get => (double)GetValue(ZoomLevelProperty);
            set => SetValue(ZoomLevelProperty, Math.Min(ZOOMLEVELMAX, Math.Max(ZOOMLEVELMIN, value)));
        }

        public double ZoomLevelIncrement
        {
            get => (double)GetValue(ZoomLevelIncrementProperty);
            set => SetValue(ZoomLevelIncrementProperty, value);
        }

        IBrowserAdapter IWebBrowserInternal.BrowserAdapter
        {
            get => ((IWebBrowserInternal)browser).BrowserAdapter;
        }

        bool IWebBrowserInternal.HasParent
        {
            get => ((IWebBrowserInternal)browser).HasParent;
            set => ((IWebBrowserInternal)browser).HasParent = value;
        }



        public ICommand BackCommand { get; }

        public ICommand ForwardCommand { get; }

        public ICommand ReloadCommand { get; }

        public ICommand PrintCommand { get; }

        public ICommand ZoomInCommand { get; }

        public ICommand ZoomOutCommand { get; }

        public ICommand ZoomResetCommand { get; }

        public ICommand ViewSourceCommand { get; }

        public ICommand StopCommand { get; }

        public ICommand CutCommand { get; }

        public ICommand CopyCommand { get; }

        public ICommand PasteCommand { get; }

        public ICommand SelectAllCommand { get; }

        public ICommand UndoCommand { get; }

        public ICommand RedoCommand { get; }




        public static readonly DependencyProperty CanGoBackProperty;

        public static readonly DependencyProperty CanGoForwardProperty;

        public static readonly DependencyProperty AddressProperty;

        public static readonly DependencyProperty IsLoadingProperty;

        public static readonly DependencyProperty IsBrowserInitializedProperty;

        public static readonly DependencyProperty TitleProperty;

        public static readonly DependencyProperty ZoomLevelProperty;

        public static readonly DependencyProperty ZoomLevelIncrementProperty;

        public static readonly DependencyProperty StatusTextProperty;



        private static void OnZoomLevelPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ChromiumWebBrowser chromiumWebBrowser = (ChromiumWebBrowser)sender;
            chromiumWebBrowser.browser.SetZoomLevel((double)e.NewValue);
        }

        private static void OnAddressPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ChromiumWebBrowser chromiumWebBrowser = (ChromiumWebBrowser)sender;

            if (!chromiumWebBrowser.onNotifyAddressChanged)
            {
                chromiumWebBrowser.browser.Load((string)e.NewValue);
            }
        }



        static ChromiumWebBrowser()
        {
            CanGoBackProperty = DependencyProperty.Register(
                nameof(CanGoBack), typeof(bool), typeof(ChromiumWebBrowser));

            CanGoForwardProperty = DependencyProperty.Register(
                nameof(CanGoForward), typeof(bool), typeof(ChromiumWebBrowser));

            AddressProperty = DependencyProperty.Register(
                nameof(Address), typeof(string), typeof(ChromiumWebBrowser), new UIPropertyMetadata(null, OnAddressPropertyChanged));

            IsLoadingProperty = DependencyProperty.Register(
                nameof(IsLoading), typeof(bool), typeof(ChromiumWebBrowser), new PropertyMetadata(false));

            IsBrowserInitializedProperty = DependencyProperty.Register(
                nameof(IsBrowserInitialized), typeof(bool), typeof(ChromiumWebBrowser), new PropertyMetadata(false));

            TitleProperty = DependencyProperty.Register(
                nameof(Title), typeof(string), typeof(ChromiumWebBrowser), new PropertyMetadata(null));

            ZoomLevelProperty = DependencyProperty.Register(
                nameof(ZoomLevel), typeof(double), typeof(ChromiumWebBrowser), new UIPropertyMetadata(ZOOMLEVELDEFAULT, OnZoomLevelPropertyChanged));

            ZoomLevelIncrementProperty = DependencyProperty.Register(
                nameof(ZoomLevelIncrement), typeof(double), typeof(ChromiumWebBrowser), new PropertyMetadata(ZOOMLEVELINCREMENTDEFAULT));

            StatusTextProperty = DependencyProperty.Register(
                nameof(StatusText), typeof(string), typeof(ChromiumWebBrowser), new PropertyMetadata(null));
        }

        public ChromiumWebBrowser()
        {
#pragma warning disable CS0618 // type or member is obsolete
            browser = new CefSharp.WinForms.ChromiumWebBrowser();
            browser.CreateControl();
#pragma warning restore CS0618 // type or member is obsolete

            BackCommand = new DelegateCommand(this.Back) { CanExecute = false };
            ForwardCommand = new DelegateCommand(this.Forward) { CanExecute = false };
            ReloadCommand = new DelegateCommand(this.Reload) { CanExecute = false };
            PrintCommand = new DelegateCommand(this.Print);
            ZoomOutCommand = new DelegateCommand(ZoomOut);
            ZoomInCommand = new DelegateCommand(ZoomIn);
            ZoomResetCommand = new DelegateCommand(ZoomReset);
            ViewSourceCommand = new DelegateCommand(this.ViewSource);
            StopCommand = new DelegateCommand(this.Stop) { CanExecute = false };
            CutCommand = new DelegateCommand(this.Cut);
            CopyCommand = new DelegateCommand(this.Copy);
            PasteCommand = new DelegateCommand(this.Paste);
            SelectAllCommand = new DelegateCommand(this.SelectAll);
            UndoCommand = new DelegateCommand(this.Undo);
            RedoCommand = new DelegateCommand(this.Redo);

            browser.JavascriptMessageReceived += OnJavascriptMessageReceived;
            browser.ConsoleMessage += OnConsoleMessage;
            browser.StatusMessage += OnStatusMessage;
            browser.FrameLoadStart += OnFrameLoadStart;
            browser.FrameLoadEnd += OnFrameLoadEnd;
            browser.LoadError += OnLoadError;
            browser.LoadingStateChanged += OnLoadingStateChanged;
            browser.AddressChanged += OnAddressChanged;
            browser.TitleChanged += OnTitleChanged;
            browser.IsBrowserInitializedChanged += OnIsBrowserInitializedChanged;
        }



        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            Win32.SetParent(browser.Handle, hwndParent.Handle);
            return new HandleRef(this, browser.Handle);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            browser.Dispose();
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            browser.Focus();
        }



        private void InvokeOnUIThread(Action action, bool invokeAsync = true)
        {
            if (action == null)
                return;

            if (invokeAsync)
            {
                Dispatcher.InvokeAsync(action);
            }
            else
            {
                if (Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Dispatcher.Invoke(action);
                }
            }
        }

        private void OnJavascriptMessageReceived(object sender, JavascriptMessageReceivedEventArgs e)
        {
            InvokeOnUIThread(delegate { OnJavascriptMessageReceived(e); });
        }

        protected virtual void OnJavascriptMessageReceived(JavascriptMessageReceivedEventArgs e)
        {
            JavascriptMessageReceived?.Invoke(this, e);
        }

        private void OnConsoleMessage(object sender, ConsoleMessageEventArgs e)
        {
            InvokeOnUIThread(delegate { OnConsoleMessage(e); });
        }

        protected virtual void OnConsoleMessage(ConsoleMessageEventArgs e)
        {
            ConsoleMessage?.Invoke(this, e);
        }

        private void OnStatusMessage(object sender, StatusMessageEventArgs e)
        {
            InvokeOnUIThread(delegate { OnStatusMessage(e); });
        }

        protected virtual void OnStatusMessage(StatusMessageEventArgs e)
        {
            SetCurrentValue(StatusTextProperty, e.Value);
            StatusMessage?.Invoke(this, e);
        }

        private void OnFrameLoadStart(object sender, FrameLoadStartEventArgs e)
        {
            InvokeOnUIThread(delegate { OnFrameLoadStart(e); });
        }

        protected virtual void OnFrameLoadStart(FrameLoadStartEventArgs e)
        {
            FrameLoadStart?.Invoke(this, e);
        }

        private void OnFrameLoadEnd(object sender, FrameLoadEndEventArgs e)
        {
            InvokeOnUIThread(delegate { OnFrameLoadEnd(e); });
        }

        protected virtual void OnFrameLoadEnd(FrameLoadEndEventArgs e)
        {
            FrameLoadEnd?.Invoke(this, e);
        }

        private void OnLoadError(object sender, LoadErrorEventArgs e)
        {
            InvokeOnUIThread(delegate { OnLoadError(e); });
        }

        protected virtual void OnLoadError(LoadErrorEventArgs e)
        {
            LoadError?.Invoke(this, e);
        }

        private void OnLoadingStateChanged(object sender, LoadingStateChangedEventArgs e)
        {
            InvokeOnUIThread(delegate { OnLoadingStateChanged(e); });
        }

        protected virtual void OnLoadingStateChanged(LoadingStateChangedEventArgs e)
        {
            SetCurrentValue(CanGoForwardProperty, e.CanGoForward);
            SetCurrentValue(CanGoBackProperty, e.CanGoBack);
            SetCurrentValue(IsLoadingProperty, e.IsLoading);
            ((DelegateCommand)ForwardCommand).CanExecute = e.CanGoForward;
            ((DelegateCommand)BackCommand).CanExecute = e.CanGoBack;
            ((DelegateCommand)ReloadCommand).CanExecute = e.CanReload;
            ((DelegateCommand)StopCommand).CanExecute = !e.CanReload;
            LoadingStateChanged?.Invoke(this, e);
        }

        private void OnAddressChanged(object sender, AddressChangedEventArgs e)
        {
            InvokeOnUIThread(delegate { OnAddressChanged(e); });
        }

        protected virtual void OnAddressChanged(AddressChangedEventArgs e)
        {
            lock (onNotifyAddressChangedLock)
            {
                onNotifyAddressChanged = true;
                SetCurrentValue(AddressProperty, e.Address);
                onNotifyAddressChanged = false;
            }
            AddressChanged?.Invoke(this, e);
        }

        private void OnTitleChanged(object sender, TitleChangedEventArgs e)
        {
            InvokeOnUIThread(delegate { OnTitleChanged(e); });
        }

        protected virtual void OnTitleChanged(TitleChangedEventArgs e)
        {
            SetCurrentValue(TitleProperty, e.Title);
            TitleChanged?.Invoke(this, e);
        }

        private void OnIsBrowserInitializedChanged(object sender, EventArgs e)
        {
            InvokeOnUIThread(delegate { OnIsBrowserInitializedChanged(e); });
        }

        protected virtual void OnIsBrowserInitializedChanged(EventArgs e)
        {
            SetCurrentValue(IsBrowserInitializedProperty, browser.IsBrowserInitialized);
            IsBrowserInitializedChanged?.Invoke(this, e);
        }



        public void Load(string url)
        {
            SetCurrentValue(AddressProperty, url);
        }

        public void ZoomOut()
        {
            SetCurrentZoomLevel(ZoomLevel - ZoomLevelIncrement);
        }

        public void ZoomIn()
        {
            SetCurrentZoomLevel(ZoomLevel + ZoomLevelIncrement);
        }

        public void ZoomReset()
        {
            SetCurrentZoomLevel(ZOOMLEVELDEFAULT);
        }

        public IBrowser GetBrowser()
        {
            return browser.GetBrowser();
        }

        private void SetCurrentZoomLevel(double zoomLevel)
        {
            SetCurrentValue(ZoomLevelProperty, Math.Min(ZOOMLEVELMAX, Math.Max(ZOOMLEVELMIN, zoomLevel)));
        }



        void IWebBrowserInternal.OnAfterBrowserCreated(IBrowser browser)
        {
            ((IWebBrowserInternal)this.browser).OnAfterBrowserCreated(browser);
        }

        void IWebBrowserInternal.SetAddress(AddressChangedEventArgs args)
        {
            ((IWebBrowserInternal)browser).SetAddress(args);
        }

        void IWebBrowserInternal.SetLoadingStateChange(LoadingStateChangedEventArgs args)
        {
            ((IWebBrowserInternal)browser).SetLoadingStateChange(args);
        }

        void IWebBrowserInternal.SetTitle(TitleChangedEventArgs args)
        {
            ((IWebBrowserInternal)browser).SetTitle(args);
        }

        void IWebBrowserInternal.SetTooltipText(string tooltipText)
        {
            ((IWebBrowserInternal)browser).SetTooltipText(tooltipText);
        }

        void IWebBrowserInternal.SetJavascriptMessageReceived(JavascriptMessageReceivedEventArgs args)
        {
            ((IWebBrowserInternal)browser).SetJavascriptMessageReceived(args);
        }

        void IWebBrowserInternal.OnFrameLoadStart(FrameLoadStartEventArgs args)
        {
            ((IWebBrowserInternal)browser).OnFrameLoadStart(args);
        }

        void IWebBrowserInternal.OnFrameLoadEnd(FrameLoadEndEventArgs args)
        {
            ((IWebBrowserInternal)browser).OnFrameLoadEnd(args);
        }

        void IWebBrowserInternal.OnConsoleMessage(ConsoleMessageEventArgs args)
        {
            ((IWebBrowserInternal)browser).OnConsoleMessage(args);
        }

        void IWebBrowserInternal.OnStatusMessage(StatusMessageEventArgs args)
        {
            ((IWebBrowserInternal)browser).OnStatusMessage(args);
        }

        void IWebBrowserInternal.OnLoadError(LoadErrorEventArgs args)
        {
            ((IWebBrowserInternal)browser).OnLoadError(args);
        }

        void IWebBrowserInternal.SetCanExecuteJavascriptOnMainFrame(long frameId, bool canExecute)
        {
            ((IWebBrowserInternal)browser).SetCanExecuteJavascriptOnMainFrame(frameId, canExecute);
        }
    }
}
