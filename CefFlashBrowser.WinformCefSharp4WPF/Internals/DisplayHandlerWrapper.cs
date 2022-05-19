using CefSharp;
using CefSharp.Structs;
using System.Collections.Generic;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class DisplayHandlerWrapper : IDisplayHandler, IHandlerWrapper<IDisplayHandler>
    {
        public IDisplayHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public DisplayHandlerWrapper(IDisplayHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public void OnAddressChanged(IWebBrowser chromiumWebBrowser, AddressChangedEventArgs addressChangedArgs)
        {
            Handler.OnAddressChanged(TargetBrowser, addressChangedArgs);
        }

        public bool OnAutoResize(IWebBrowser chromiumWebBrowser, IBrowser browser, Size newSize)
        {
            return Handler.OnAutoResize(TargetBrowser, browser, newSize);
        }

        public bool OnConsoleMessage(IWebBrowser chromiumWebBrowser, ConsoleMessageEventArgs consoleMessageArgs)
        {
            return Handler.OnConsoleMessage(TargetBrowser, consoleMessageArgs);
        }

        public void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls)
        {
            Handler.OnFaviconUrlChange(TargetBrowser, browser, urls);
        }

        public void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen)
        {
            Handler.OnFullscreenModeChange(TargetBrowser, browser, fullscreen);
        }

        public void OnLoadingProgressChange(IWebBrowser chromiumWebBrowser, IBrowser browser, double progress)
        {
            Handler.OnLoadingProgressChange(TargetBrowser, browser, progress);
        }

        public void OnStatusMessage(IWebBrowser chromiumWebBrowser, StatusMessageEventArgs statusMessageArgs)
        {
            Handler.OnStatusMessage(TargetBrowser, statusMessageArgs);
        }

        public void OnTitleChanged(IWebBrowser chromiumWebBrowser, TitleChangedEventArgs titleChangedArgs)
        {
            Handler.OnTitleChanged(TargetBrowser, titleChangedArgs);
        }

        public bool OnTooltipChanged(IWebBrowser chromiumWebBrowser, ref string text)
        {
            return Handler.OnTooltipChanged(TargetBrowser, ref text);
        }
    }
}
