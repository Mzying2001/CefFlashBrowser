using CefSharp;
using CefFlashBrowser.Browser.UserScripts;
using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Domain;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

public static class UserScriptMenuProbe
{
    private static UserScriptMenuService Menus(IWebBrowser browser)
    {
        return (UserScriptMenuService)typeof(UserScriptContextMenuHandler)
            .GetField("_menus", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(browser.MenuHandler);
    }

    public static async Task<IMenuRegistration[]> RegisterManyAsync(IWebBrowser browser, ScriptKey key, string prefix, int count)
    {
        var registrations = new List<IMenuRegistration>();
        try
        {
            for (var index = 0; index < count; index++)
                registrations.Add(await Menus(browser).RegisterAsync(
                    new MenuCommandRequest(key, prefix + index, null, null, true, null, false),
                    invocation => { }, CancellationToken.None));

            return registrations.ToArray();
        }
        catch
        {
            foreach (var registration in registrations)
                registration.Dispose();
            throw;
        }
    }

    public static Task<RegressionMenuModelProxy> BuildWithBaseAsync(IWebBrowser browser, bool empty, bool separator)
    {
        return Cef.UIThreadTaskFactory.StartNew(() =>
        {
            var model = new RegressionMenuModelProxy();
            var nativeModel = (IMenuModel)model.GetTransparentProxy();
            if (empty)
                nativeModel.Clear();
            if (separator)
                nativeModel.AddSeparator();

            var count = nativeModel.Count;
            var handler = new UserScriptContextMenuHandler(new CefFlashBrowser.FlashBrowser.Handlers.ContextMenuHandler(),
                Menus(browser), () => count, () => model.LimitReports++);
            handler.OnBeforeContextMenu(browser, browser.GetBrowser(), null,
                (IContextMenuParams)Parameters().GetTransparentProxy(), nativeModel);
            return model;
        });
    }

    public static ContextMenu Render(RegressionMenuModelProxy model)
    {
        var handler = typeof(CefFlashBrowser.Browser.Handlers.ContextMenuHandler);
        var info = handler.Assembly.GetType("CefFlashBrowser.Browser.Handlers.CefMenuItemInfo")
            .GetMethod("GetMenuItemInfoList").Invoke(null, new[] { model.GetTransparentProxy() });
        var items = handler.GetMethod("GetMenuItems", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new[] { info, null });
        return new ContextMenu { ItemsSource = (IEnumerable)items };
    }

    public static Task<RegressionMenuModelProxy> BuildOverflowAsync(IWebBrowser browser)
    {
        return Cef.UIThreadTaskFactory.StartNew(() =>
        {
            var handler = new UserScriptContextMenuHandler(new OverflowHandler(), Menus(browser), () => 2, () => { });
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
