using CefSharp;
using CefSharp.Structs;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class FindHandler : IFindHandler
    {
        public virtual void OnFindResult(IWebBrowser chromiumWebBrowser, IBrowser browser, int identifier, int count, Rect selectionRect, int activeMatchOrdinal, bool finalUpdate)
        {
        }
    }
}
