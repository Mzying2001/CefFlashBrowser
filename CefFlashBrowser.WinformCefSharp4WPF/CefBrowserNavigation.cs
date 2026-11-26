using CefSharp;
using System;
using System.Threading.Tasks;

namespace CefFlashBrowser.WinformCefSharp4WPF
{
    public static class CefBrowserNavigation
    {
        public static Task RunAsync(IWebBrowser browser, Action<IBrowser> navigate)
        {
            if (browser.IsDisposed || !browser.IsBrowserInitialized)
                return Task.CompletedTask;

            // The bundled CEF 87 build can crash when navigation commands run off its UI thread.
            return Cef.UIThreadTaskFactory.StartNew(() =>
            {
                if (browser.IsDisposed || !browser.IsBrowserInitialized)
                    return;

                try
                {
                    var nativeBrowser = browser.GetBrowser();

                    if (nativeBrowser != null && !nativeBrowser.IsDisposed)
                        navigate(nativeBrowser);
                }
                catch (ObjectDisposedException)
                {
                    // The browser can finish closing while the command is queued.
                }
            });
        }
    }
}
