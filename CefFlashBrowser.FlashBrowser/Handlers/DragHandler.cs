using CefSharp;
using CefSharp.Enums;
using System.Collections.Generic;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class DragHandler : IDragHandler
    {
        public virtual bool OnDragEnter(IWebBrowser chromiumWebBrowser, IBrowser browser, IDragData dragData, DragOperationsMask mask)
        {
            return false;
        }

        public virtual void OnDraggableRegionsChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IList<DraggableRegion> regions)
        {
        }
    }
}
