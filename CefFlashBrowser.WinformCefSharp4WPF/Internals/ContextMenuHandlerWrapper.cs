using CefSharp;

namespace CefFlashBrowser.WinformCefSharp4WPF.Internals
{
    internal class ContextMenuHandlerWrapper : IContextMenuHandler, IHandlerWrapper<IContextMenuHandler>
    {
        public IContextMenuHandler Handler { get; }

        public IWebBrowser TargetBrowser { get; }

        public ContextMenuHandlerWrapper(IContextMenuHandler handler, IWebBrowser targetBrowser)
        {
            Handler = handler;
            TargetBrowser = targetBrowser;
        }

        public void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            Handler.OnBeforeContextMenu(TargetBrowser, browser, frame, parameters, model);
        }

        public bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            return Handler.OnContextMenuCommand(TargetBrowser, browser, frame, parameters, commandId, eventFlags);
        }

        public void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
            Handler.OnContextMenuDismissed(TargetBrowser, browser, frame);
        }

        public bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            return Handler.RunContextMenu(TargetBrowser, browser, frame, parameters, model, callback);
        }
    }
}
