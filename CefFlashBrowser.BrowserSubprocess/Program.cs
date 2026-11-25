using CefFlashBrowser.Browser;
using CefSharp.BrowserSubprocess;

namespace CefFlashBrowser.BrowserSubprocess
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            SubProcess.EnableHighDPISupport();

            var handler = CoreJsProcessSettings.Enabled ? new PolyfillRenderProcessHandler() : null;
            return new WcfBrowserSubprocessExecutable().Main(args, handler);
        }
    }
}
