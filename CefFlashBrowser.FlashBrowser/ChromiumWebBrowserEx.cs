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
        private class WpfFocusHandler : Handlers.FocusHandler
        {
            public override void OnGotFocus(IWebBrowser chromiumWebBrowser, IBrowser browser)
            {
                if (browser.IsPopup)
                {
                    return; //ignore if method is called by devtools
                }

                (chromiumWebBrowser as UIElement)?.Dispatcher.Invoke(delegate
                {
                    //var element = (UIElement)chromiumWebBrowser;
                    //element.Focus();

                    DependencyObject d = (DependencyObject)chromiumWebBrowser;
                    while (LogicalTreeHelper.GetParent(d) is DependencyObject parent)
                    {
                        d = parent;
                    }
                    if (!ReferenceEquals(d, chromiumWebBrowser))
                    {
                        FocusManager.SetFocusedElement(d, (IInputElement)chromiumWebBrowser);
                    }
                });
            }
        }

        private class WpfKeyboardHandler : Handlers.KeyboardHandler
        {
            public override bool OnPreKeyEvent(IWebBrowser chromiumWebBrowser, IBrowser browser, KeyType type, int windowsKeyCode, int nativeKeyCode, CefEventFlags modifiers, bool isSystemKey, ref bool isKeyboardShortcut)
            {
                if (type != KeyType.RawKeyDown)
                    return false;

                bool result = false;

                (chromiumWebBrowser as UIElement)?.Dispatcher.Invoke(delegate
                {
                    UIElement element = (UIElement)chromiumWebBrowser;
                    PresentationSource source = PresentationSource.FromVisual(element);

                    Key key = KeyInterop.KeyFromVirtualKey(windowsKeyCode);
                    KeyEventArgs args = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key) { RoutedEvent = Keyboard.KeyDownEvent, };
                    element.RaiseEvent(args);

                    result = args.Handled;
                });

                return result;
            }
        }

        class MyDisplayHandler : Handlers.DisplayHandler
        {
            public override void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen)
            {
                if (chromiumWebBrowser is ChromiumWebBrowserEx browserEx)
                {
                    browserEx.Dispatcher.Invoke(() => browserEx.OnFullscreenModeChanged(fullscreen));
                }
            }

            public override void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls)
            {
                if (chromiumWebBrowser is ChromiumWebBrowserEx browserEx)
                {
                    browserEx.Dispatcher.Invoke(() => browserEx.OnFaviconUrlChanged(urls));
                }
            }
        }



        public event EventHandler FullscreenModeChanged;
        public event EventHandler FaviconUrlChanged;

        public ICommand LoadUrlCommand { get; }
        public ICommand CloseBrowserCommand { get; }
        public ICommand ShowDevToolsCommand { get; }
        public ICommand CloseDevToolsCommand { get; }

        public static readonly DependencyProperty FullscreenModeProperty;
        public static readonly DependencyProperty FaviconUrlProperty;

        public bool FullscreenMode
        {
            get { return (bool)GetValue(FullscreenModeProperty); }
        }

        public string FaviconUrl
        {
            get { return (string)GetValue(FaviconUrlProperty); }
        }



        static ChromiumWebBrowserEx()
        {
            FullscreenModeProperty = DependencyProperty.Register(
                nameof(FullscreenMode), typeof(bool), typeof(ChromiumWebBrowserEx), new PropertyMetadata(false));

            FaviconUrlProperty = DependencyProperty.Register(
                nameof(FaviconUrl), typeof(string), typeof(ChromiumWebBrowserEx), new PropertyMetadata(null));
        }

        public ChromiumWebBrowserEx()
        {
            FocusHandler = new WpfFocusHandler();
            KeyboardHandler = new WpfKeyboardHandler();
            DisplayHandler = new MyDisplayHandler();

            LoadUrlCommand = new DelegateCommand<string>(Load);
            CloseBrowserCommand = new DelegateCommand<bool>(CloseBrowser);
            ShowDevToolsCommand = new DelegateCommand(ShowDevTools);
            CloseDevToolsCommand = new DelegateCommand(CloseDevTools);
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
            SetValue(FullscreenModeProperty, fullscreen);
            FullscreenModeChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnFaviconUrlChanged(IList<string> urls)
        {
            SetValue(FaviconUrlProperty, urls != null && urls.Count > 0 ? urls[0] : null);
            FaviconUrlChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override void OnFrameLoadStart(FrameLoadStartEventArgs e)
        {
            base.OnFrameLoadStart(e);

            if (e.Frame.IsMain)
            {
                OnFaviconUrlChanged(null);
            }
        }
    }
}
