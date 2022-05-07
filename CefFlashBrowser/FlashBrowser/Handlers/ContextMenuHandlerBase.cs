using CefFlashBrowser.Models;
using CefSharp;
using System.Collections.Generic;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public abstract class ContextMenuHandlerBase : IContextMenuHandler
    {
        void IContextMenuHandler.OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            OnBeforeContextMenu(chromiumWebBrowser, browser, frame, parameters, model);
        }

        public virtual void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
        }

        bool IContextMenuHandler.OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            return OnContextMenuCommand(chromiumWebBrowser, browser, frame, parameters, commandId, eventFlags);
        }

        public virtual bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            return false;
        }

        void IContextMenuHandler.OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
            OnContextMenuDismissed(chromiumWebBrowser, browser, frame);
        }

        public virtual void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
        }

        bool IContextMenuHandler.RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            return RunContextMenu(chromiumWebBrowser, browser, frame, parameters, model, callback);
        }

        public virtual bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            return false;
        }

        protected static IList<CefMenuItemInfo> GetMenuItemInfoList(IMenuModel model)
        {
            if (model == null)
            {
                return null;
            }

            List<CefMenuItemInfo> list = new List<CefMenuItemInfo>();

            for (var i = 0; i < model.Count; i++)
            {
                list.Add(new CefMenuItemInfo
                {
                    Header = model.GetLabelAt(i),
                    IsEnable = model.IsEnabledAt(i),
                    IsChecked = model.IsCheckedAt(i),
                    CommandID = model.GetCommandIdAt(i),
                    SubMenuItemInfos = GetMenuItemInfoList(model.GetSubMenuAt(i))
                });
            }

            return list;
        }
    }
}
