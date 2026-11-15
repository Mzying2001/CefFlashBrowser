using CefFlashBrowser.Data;
using CefFlashBrowser.ViewModels;
using CefSharp;
using System;
using System.Collections.Generic;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptContextMenuHandler : IContextMenuHandler
    {
        public const int ManualLimit = 1024;
        private readonly UserScriptMenuService _menus;
        private readonly Func<int> _originalCount;
        private readonly Action _reportLimit;
        private readonly Dictionary<CefMenuCommand, UserScriptMenuCommand> _commands = new Dictionary<CefMenuCommand, UserScriptMenuCommand>();
        public IContextMenuHandler Inner { get; }

        public UserScriptContextMenuHandler(IContextMenuHandler inner, UserScriptMenuService menus, Func<int> originalCount, Action reportLimit)
        {
            Inner = inner;
            _menus = menus;
            _originalCount = originalCount;
            _reportLimit = reportLimit;
        }

        public void OnBeforeContextMenu(IWebBrowser browser, IBrowser native, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            _commands.Clear();
            Inner.OnBeforeContextMenu(browser, native, frame, parameters, model);
            if (GlobalData.Settings.DisableBrowserContextMenu)
            {
                model.Clear();
                return;
            }
            var first = (int)CefMenuCommand.UserFirst;
            var last = (int)CefMenuCommand.UserLast - 4;
            var limit = Math.Min(ManualLimit, Math.Max(0, last - first + 1));
            var removed = false;
            for (var index = model.Count - 1; index >= _originalCount(); index--)
            {
                var id = (int)model.GetCommandIdAt(index);
                if (id != (int)CefMenuCommand.NotFound && (id < first || id >= first + limit))
                {
                    model.RemoveAt(index);
                    removed = true;
                }
            }
            if (removed) _reportLimit();
            var command = first + limit;
            foreach (var item in _menus.GetCommands())
            {
                if (command > last) { _reportLimit(); break; }
                var id = (CefMenuCommand)command++;
                _commands.Add(id, item);
                model.AddItem(id, item.Name);
            }
        }

        public bool OnContextMenuCommand(IWebBrowser browser, IBrowser native, IFrame frame, IContextMenuParams parameters, CefMenuCommand id, CefEventFlags flags)
        {
            if (GlobalData.Settings.DisableBrowserContextMenu) return true;
            return _commands.TryGetValue(id, out var command) ? _menus.Invoke(command, frame, flags)
                : Inner.OnContextMenuCommand(browser, native, frame, parameters, id, flags);
        }

        public void OnContextMenuDismissed(IWebBrowser browser, IBrowser native, IFrame frame)
        {
            _commands.Clear();
            Inner.OnContextMenuDismissed(browser, native, frame);
        }

        public bool RunContextMenu(IWebBrowser browser, IBrowser native, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            if (GlobalData.Settings.DisableBrowserContextMenu || model.Count == 0)
            {
                callback.Cancel();
                return true;
            }
            return Inner.RunContextMenu(browser, native, frame, parameters, model, callback);
        }
    }
}
