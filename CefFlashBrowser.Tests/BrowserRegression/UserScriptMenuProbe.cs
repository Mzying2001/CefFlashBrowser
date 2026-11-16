using CefSharp;
using CefFlashBrowser.Browser.UserScripts;
using System;
using System.Threading.Tasks;

public static class UserScriptMenuProbe
{
    public static Task<RegressionMenuModelProxy> BuildOverflowAsync(IWebBrowser browser)
    {
        return Cef.UIThreadTaskFactory.StartNew(() =>
        {
            var outer = (UserScriptContextMenuHandler)browser.MenuHandler;
            var menus = (UserScriptMenuService)typeof(UserScriptContextMenuHandler)
                .GetField("_menus", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(outer);
            var handler = new UserScriptContextMenuHandler(new OverflowHandler(), menus, () => 2, () => { });
            var model = new RegressionMenuModelProxy();

            using (var frame = browser.GetBrowser().MainFrame)
                handler.OnBeforeContextMenu(browser, browser.GetBrowser(), frame,
                    (IContextMenuParams)Parameters().GetTransparentProxy(), (IMenuModel)model.GetTransparentProxy());

            return model;
        });
    }

    public static Task<RegressionMenuModelProxy> BuildAsync(IWebBrowser browser)
    {
        return Cef.UIThreadTaskFactory.StartNew(() =>
        {
            var model = new RegressionMenuModelProxy();
            var parameters = Parameters();

            using (var frame = browser.GetBrowser().MainFrame)
                browser.MenuHandler.OnBeforeContextMenu(browser, browser.GetBrowser(), frame,
                    (IContextMenuParams)parameters.GetTransparentProxy(), (IMenuModel)model.GetTransparentProxy());

            return model;
        });
    }

    public static Task<bool> InvokeAsync(IWebBrowser browser, CefMenuCommand command)
    {
        return Cef.UIThreadTaskFactory.StartNew(() =>
        {
            var parameters = Parameters();

            using (var frame = browser.GetBrowser().MainFrame)
                return browser.MenuHandler.OnContextMenuCommand(browser, browser.GetBrowser(), frame,
                    (IContextMenuParams)parameters.GetTransparentProxy(), command, CefEventFlags.None);
        });
    }

    public static Task DismissAsync(IWebBrowser browser)
    {
        return Cef.UIThreadTaskFactory.StartNew(() =>
        {
            using (var frame = browser.GetBrowser().MainFrame)
                browser.MenuHandler.OnContextMenuDismissed(browser, browser.GetBrowser(), frame);
        });
    }

    private static RegressionContextMenuParamsProxy Parameters()
    {
        return new RegressionContextMenuParamsProxy { SelectionText = "menu probe", LinkUrl = "about:blank" };
    }

    private sealed class OverflowHandler : IContextMenuHandler
    {
        public void OnBeforeContextMenu(IWebBrowser browser, IBrowser native, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            model.AddItem(CefFlashBrowser.Browser.Handlers.ContextMenuHandler.Search, "Application search");
            model.AddSeparator();

            for (var index = 0; index < 3000; index++)
                model.AddItem((CefMenuCommand)((int)CefMenuCommand.UserFirst + index), "Manual source " + index);
        }

        public bool OnContextMenuCommand(IWebBrowser browser, IBrowser native, IFrame frame, IContextMenuParams parameters, CefMenuCommand id, CefEventFlags flags)
        {
            return false;
        }

        public void OnContextMenuDismissed(IWebBrowser browser, IBrowser native, IFrame frame)
        {
        }

        public bool RunContextMenu(IWebBrowser browser, IBrowser native, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            return false;
        }
    }
}
