using CefSharp;
using CefSharp.Enums;
using System.Collections.Generic;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class DragHandlerWrapper : IDragHandler, IHandlerWrapper<IDragHandler>
    {
        public IDragHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public DragHandlerWrapper(IDragHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public bool OnDragEnter(IWebBrowser chromiumWebBrowser, IBrowser browser, IDragData dragData, DragOperationsMask mask)
        {
            return Handler.OnDragEnter(TargetBrowser, browser, dragData, mask);
        }

        public void OnDraggableRegionsChanged(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IList<DraggableRegion> regions)
        {
            Handler.OnDraggableRegionsChanged(TargetBrowser, browser, frame, regions);
        }
    }
}
