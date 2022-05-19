using CefSharp;
using CefSharp.Structs;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class FindHandlerWrapper : IFindHandler, IHandlerWrapper<IFindHandler>
    {
        public IFindHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public FindHandlerWrapper(IFindHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public void OnFindResult(IWebBrowser chromiumWebBrowser, IBrowser browser, int identifier, int count, Rect selectionRect, int activeMatchOrdinal, bool finalUpdate)
        {
            Handler.OnFindResult(TargetBrowser, browser, identifier, count, selectionRect, activeMatchOrdinal, finalUpdate);
        }
    }
}
