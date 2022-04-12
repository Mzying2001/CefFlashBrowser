using CefSharp;
using CefSharp.Internals;
using SimpleMvvm.Command;
using System;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Input;

namespace CefFlashBrowser.FlashBrowser
{
    public class WfChromiumWebBrowser : WindowsFormsHost, IWebBrowserInternal, IWebBrowser, IDisposable
    {
        protected CefSharp.WinForms.ChromiumWebBrowser browser;

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

        public IDialogHandler DialogHandler
        {
            get => browser.DialogHandler;
            set => browser.DialogHandler = value;
        }

        public IRequestHandler RequestHandler
        {
            get => browser.RequestHandler;
            set => browser.RequestHandler = value;
        }

        public IDisplayHandler DisplayHandler
        {
            get => browser.DisplayHandler;
            set => browser.DisplayHandler = value;
        }

        public ILoadHandler LoadHandler
        {
            get => browser.LoadHandler;
            set => browser.LoadHandler = value;
        }

        public ILifeSpanHandler LifeSpanHandler
        {
            get => browser.LifeSpanHandler;
            set => browser.LifeSpanHandler = value;
        }

        public IKeyboardHandler KeyboardHandler
        {
            get => browser.KeyboardHandler;
            set => browser.KeyboardHandler = value;
        }

        public IJsDialogHandler JsDialogHandler
        {
            get => browser.JsDialogHandler;
            set => browser.JsDialogHandler = value;
        }

        public IDragHandler DragHandler
        {
            get => browser.DragHandler;
            set => browser.DragHandler = value;
        }

        public IDownloadHandler DownloadHandler
        {
            get => browser.DownloadHandler;
            set => browser.DownloadHandler = value;
        }

        public IContextMenuHandler MenuHandler
        {
            get => browser.MenuHandler;
            set => browser.MenuHandler = value;
        }

        public IFocusHandler FocusHandler
        {
            get => browser.FocusHandler;
            set => browser.FocusHandler = value;
        }

        public IResourceRequestHandlerFactory ResourceRequestHandlerFactory
        {
            get => browser.ResourceRequestHandlerFactory;
            set => browser.ResourceRequestHandlerFactory = value;
        }

        public IRenderProcessMessageHandler RenderProcessMessageHandler
        {
            get => browser.RenderProcessMessageHandler;
            set => browser.RenderProcessMessageHandler = value;
        }

        public IFindHandler FindHandler
        {
            get => browser.FindHandler;
            set => browser.FindHandler = value;
        }

        public string TooltipText => browser.TooltipText;

        public bool CanExecuteJavascriptInMainFrame => browser.CanExecuteJavascriptInMainFrame;

        public IRequestContext RequestContext => browser.RequestContext;

        public bool IsBrowserInitialized => browser.IsBrowserInitialized;

        public bool IsDisposed => browser.IsDisposed;

        public bool IsLoading => browser.IsLoading;

        public bool CanGoBack => browser.CanGoBack;

        public bool CanGoForward => browser.CanGoForward;

        public string Address => browser.Address;

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

        public double ZoomLevel
        {
            get { return (double)GetValue(ZoomLevelProperty); }
            set { SetValue(ZoomLevelProperty, value); }
        }

        public double ZoomLevelIncrement
        {
            get { return (double)GetValue(ZoomLevelIncrementProperty); }
            set { SetValue(ZoomLevelIncrementProperty, value); }
        }

        public string Title => (string)GetValue(TitleProperty);

        public static readonly DependencyProperty CanGoBackProperty;

        public static readonly DependencyProperty CanGoForwardProperty;

        public static readonly DependencyProperty AddressProperty;

        public static readonly DependencyProperty IsLoadingProperty;

        public static readonly DependencyProperty IsBrowserInitializedProperty;

        public static readonly DependencyProperty TitleProperty;

        public static readonly DependencyProperty ZoomLevelProperty;

        public static readonly DependencyProperty ZoomLevelIncrementProperty;

        public IAccessibilityHandler AccessibilityHandler { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        IBrowserAdapter IWebBrowserInternal.BrowserAdapter => ((IWebBrowserInternal)browser).BrowserAdapter;

        bool IWebBrowserInternal.HasParent
        {
            get => ((IWebBrowserInternal)browser).HasParent;
            set => ((IWebBrowserInternal)browser).HasParent = value;
        }





        public new CefSharp.WinForms.ChromiumWebBrowser Child
        {
            get => (CefSharp.WinForms.ChromiumWebBrowser)base.Child;
            protected set => base.Child = value;
        }





        static WfChromiumWebBrowser()
        {
            CanGoBackProperty = DependencyProperty.Register("CanGoBack", typeof(bool), typeof(WfChromiumWebBrowser));

            CanGoForwardProperty = DependencyProperty.Register("CanGoForward", typeof(bool), typeof(WfChromiumWebBrowser));

            AddressProperty = DependencyProperty.Register("Address", typeof(string), typeof(WfChromiumWebBrowser), new UIPropertyMetadata(null));

            IsLoadingProperty = DependencyProperty.Register("IsLoading", typeof(bool), typeof(WfChromiumWebBrowser), new PropertyMetadata(false));

            IsBrowserInitializedProperty = DependencyProperty.Register("IsBrowserInitialized", typeof(bool), typeof(WfChromiumWebBrowser), new PropertyMetadata(false));

            TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(WfChromiumWebBrowser), new PropertyMetadata(null));

            ZoomLevelProperty = DependencyProperty.Register("ZoomLevel", typeof(double), typeof(WfChromiumWebBrowser), new UIPropertyMetadata(0.0, (s, e) =>
            {
                WfChromiumWebBrowser wfChromiumWebBrowser = (WfChromiumWebBrowser)s;
                wfChromiumWebBrowser.browser.SetZoomLevel((double)e.NewValue);
            }));

            ZoomLevelIncrementProperty = DependencyProperty.Register("ZoomLevelIncrement", typeof(double), typeof(WfChromiumWebBrowser), new PropertyMetadata(0.1));
        }

        public WfChromiumWebBrowser()
        {
            browser = new CefSharp.WinForms.ChromiumWebBrowser();
            Child = browser;

            browser.AddressChanged += (s, e) => Dispatcher.Invoke(() =>
            {
                SetValue(AddressProperty, e.Address);
            });

            browser.LoadingStateChanged += (s, e) => Dispatcher.Invoke(() =>
            {
                SetValue(CanGoForwardProperty, e.CanGoForward);
                SetValue(CanGoBackProperty, e.CanGoBack);
                SetValue(IsLoadingProperty, e.IsLoading);

                ((DelegateCommand)ForwardCommand).CanExecute = e.CanGoForward;
                ((DelegateCommand)BackCommand).CanExecute = e.CanGoBack;
                ((DelegateCommand)ReloadCommand).CanExecute = e.CanReload;
                ((DelegateCommand)StopCommand).CanExecute = !e.CanReload;
            });

            browser.IsBrowserInitializedChanged += (s, e) => Dispatcher.Invoke(() =>
            {
                SetValue(IsBrowserInitializedProperty, ((CefSharp.WinForms.ChromiumWebBrowser)s).IsBrowserInitialized);
            });

            browser.TitleChanged += (s, e) => Dispatcher.Invoke(() =>
            {
                SetValue(TitleProperty, e.Title);
            });

            browser.JavascriptMessageReceived += (s, e) =>
            {
                JavascriptMessageReceived?.Invoke(this, e);
            };

            browser.ConsoleMessage += (s, e) =>
            {
                ConsoleMessage?.Invoke(this, e);
            };

            browser.StatusMessage += (s, e) =>
            {
                StatusMessage?.Invoke(this, e);
            };

            browser.FrameLoadStart += (s, e) =>
            {
                FrameLoadStart?.Invoke(this, e);
            };

            browser.FrameLoadEnd += (s, e) =>
            {
                FrameLoadEnd?.Invoke(this, e);
            };

            browser.LoadError += (s, e) =>
            {
                LoadError?.Invoke(this, e);
            };

            browser.LoadingStateChanged += (s, e) =>
            {
                LoadingStateChanged?.Invoke(this, e);
            };

            browser.AddressChanged += (s, e) =>
            {
                AddressChanged?.Invoke(this, e);
            };

            browser.TitleChanged += (s, e) =>
            {
                TitleChanged?.Invoke(this, e);
            };

            browser.IsBrowserInitializedChanged += (s, e) =>
            {
                IsBrowserInitializedChanged?.Invoke(this, e);
            };

            BackCommand = new DelegateCommand(browser.Back) { CanExecute = false };
            ForwardCommand = new DelegateCommand(browser.Forward) { CanExecute = false };
            ReloadCommand = new DelegateCommand(browser.Reload) { CanExecute = false };
            PrintCommand = new DelegateCommand(browser.Print);
            ZoomOutCommand = new DelegateCommand(() => ZoomLevel -= ZoomLevelIncrement);
            ZoomInCommand = new DelegateCommand(() => ZoomLevel += ZoomLevelIncrement);
            ZoomResetCommand = new DelegateCommand(() => ZoomLevel = 0.0);
            ViewSourceCommand = new DelegateCommand(browser.ViewSource);
            StopCommand = new DelegateCommand(browser.Stop) { CanExecute = false };
            CutCommand = new DelegateCommand(browser.Cut);
            CopyCommand = new DelegateCommand(browser.Copy);
            PasteCommand = new DelegateCommand(browser.Paste);
            SelectAllCommand = new DelegateCommand(browser.SelectAll);
            UndoCommand = new DelegateCommand(browser.Undo);
            RedoCommand = new DelegateCommand(browser.Redo);
        }





        public void Load(string url)
        {
            browser.Load(url);
            SetValue(AddressProperty, url);
            SetValue(TitleProperty, url);
        }

        public IBrowser GetBrowser()
        {
            return browser.GetBrowser();
        }

        void IWebBrowserInternal.OnAfterBrowserCreated(IBrowser browser)
        {
            ((IWebBrowserInternal)browser).OnAfterBrowserCreated(browser);
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

        void IWebBrowserInternal.SetCanExecuteJavascriptOnMainFrame(bool canExecute)
        {
            ((IWebBrowserInternal)browser).SetCanExecuteJavascriptOnMainFrame(canExecute);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!browser.IsDisposed)
                browser.Dispose();
        }
    }
}
