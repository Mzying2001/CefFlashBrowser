using CefFlashBrowser.Data;
using CefFlashBrowser.Models.UserScripts;
using CefFlashBrowser.ViewModels;
using CefSharp;
using Mzying2001.MonkeySharp.CefSharp;
using Mzying2001.MonkeySharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptContextMenuHandler : IContextMenuHandler
    {
        public const int ManualLimit = 1024;

        private readonly UserScriptMenuService _menus;
        private readonly Func<int> _originalCount;
        private readonly Action _reportLimit;
        private readonly CefSharpUserScriptHost _host;
        private readonly Func<ScriptKey, UserScriptMenuIcon> _getScriptIcon;
        private readonly Dictionary<CefMenuCommand, UserScriptMenuCommand> _commands = new Dictionary<CefMenuCommand, UserScriptMenuCommand>();
        private readonly Dictionary<CefMenuCommand, UserScriptMenuIcon> _scriptItems = new Dictionary<CefMenuCommand, UserScriptMenuIcon>();

        public IContextMenuHandler Inner { get; }

        public UserScriptContextMenuHandler(IContextMenuHandler inner, UserScriptMenuService menus, Func<int> originalCount, Action reportLimit)
            : this(inner, menus, originalCount, reportLimit, null, null)
        {
        }

        internal UserScriptContextMenuHandler(IContextMenuHandler inner, UserScriptMenuService menus, Func<int> originalCount,
            Action reportLimit, CefSharpUserScriptHost host, Func<ScriptKey, UserScriptMenuIcon> getScriptIcon)
        {
            Inner = inner;
            _menus = menus;
            _originalCount = originalCount;
            _reportLimit = reportLimit;
            _host = host;
            _getScriptIcon = getScriptIcon;
        }

        internal bool TryGetScriptItem(CefMenuCommand id, out UserScriptMenuIcon icon)
            => _scriptItems.TryGetValue(id, out icon);

        public void OnBeforeContextMenu(IWebBrowser browser, IBrowser native, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            _commands.Clear();
            _scriptItems.Clear();

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
            var originalCount = _originalCount();

            for (var index = model.Count - 1; index >= originalCount; index--)
            {
                var id = (int)model.GetCommandIdAt(index);

                if (id != (int)CefMenuCommand.NotFound && (id < first || id >= first + limit))
                {
                    model.RemoveAt(index);
                    removed = true;
                }
            }

            if (removed)
                _reportLimit();

            if (_host != null)
            {
                for (var index = originalCount; index < model.Count; index++)
                {
                    var id = model.GetCommandIdAt(index);

                    if (id == CefMenuCommand.NotFound || !_host.TryGetContextMenuScriptKey(id, out var key))
                        continue;

                    _scriptItems.Add(id, _getScriptIcon(key));
                }
            }

            var command = first + limit;
            var truncated = false;

            foreach (var group in _menus.GetCommands().GroupBy(item => item.ScriptKey))
            {
                // Each script needs an ID for its submenu and at least one command.
                if (command >= last)
                {
                    truncated = true;
                    break;
                }

                var parentId = (CefMenuCommand)command++;
                var firstItem = group.First();
                var submenu = model.AddSubMenu(parentId, firstItem.ScriptName);

                _scriptItems.Add(parentId, new UserScriptMenuIcon(firstItem.ScriptIconUrl, firstItem.ScriptIconInitial));

                foreach (var item in group)
                {
                    if (command > last)
                    {
                        truncated = true;
                        break;
                    }

                    var id = (CefMenuCommand)command++;

                    _commands.Add(id, item);
                    submenu.AddItem(id, item.Name);
                    _scriptItems.Add(id, null);
                }

                if (truncated)
                    break;
            }

            if (truncated)
                _reportLimit();

            if (originalCount > 0 && model.Count > originalCount &&
                model.GetCommandIdAt(originalCount - 1) != CefMenuCommand.NotFound &&
                model.GetCommandIdAt(originalCount) != CefMenuCommand.NotFound)
            {
                model.InsertSeparatorAt(originalCount);
            }
        }

        public bool OnContextMenuCommand(IWebBrowser browser, IBrowser native, IFrame frame,
            IContextMenuParams parameters, CefMenuCommand id, CefEventFlags flags)
        {
            if (GlobalData.Settings.DisableBrowserContextMenu)
                return true;

            return _commands.TryGetValue(id, out var command) ? _menus.Invoke(command, frame, flags)
                : Inner.OnContextMenuCommand(browser, native, frame, parameters, id, flags);
        }

        public void OnContextMenuDismissed(IWebBrowser browser, IBrowser native, IFrame frame)
        {
            _commands.Clear();
            _scriptItems.Clear();
            Inner.OnContextMenuDismissed(browser, native, frame);
        }

        public bool RunContextMenu(IWebBrowser browser, IBrowser native, IFrame frame,
            IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
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
