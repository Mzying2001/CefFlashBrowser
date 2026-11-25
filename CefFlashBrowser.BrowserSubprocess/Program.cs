using CefSharp.BrowserSubprocess;

namespace CefFlashBrowser.BrowserSubprocess
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            SubProcess.EnableHighDPISupport();
            return new WcfBrowserSubprocessExecutable().Main(args, new PolyfillRenderProcessHandler());
        }
    }
}
