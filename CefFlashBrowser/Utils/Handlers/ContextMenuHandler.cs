using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Command;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Utils.Handlers
{
    public class ContextMenuHandler : FlashBrowser.Handlers.ContextMenuHandler
    {
        public const CefMenuCommand OpenInNewWindow = CefMenuCommand.UserFirst + 1;
        public const CefMenuCommand Search = CefMenuCommand.UserFirst + 2;
        public const CefMenuCommand OpenSelectedUrl = CefMenuCommand.UserFirst + 3;

        public override void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            int count = 0;

            if (!string.IsNullOrWhiteSpace(parameters.SelectionText))
            {
                var selectionText = parameters.SelectionText;
                var truncatedText = selectionText.Length > 32 ? selectionText.Substring(0, 32) + "..." : selectionText;

                var header = string.Format(LanguageManager.GetString("browser_searchFor"), truncatedText.Replace('\n', ' '));
                model.InsertItemAt(0, Search, header);
                count++;

                if (UrlHelper.IsHttpUrl(selectionText))
                {
                    header = string.Format(LanguageManager.GetString("browser_openSelectedUrl"), truncatedText);
                    model.InsertCheckItemAt(0, OpenSelectedUrl, header);
                    count++;
                }
            }
            if (!string.IsNullOrWhiteSpace(parameters.LinkUrl))
            {
                var header = LanguageManager.GetString("browser_openInNewWindow");
                model.InsertCheckItemAt(0, OpenInNewWindow, header);
                count++;
            }

            if (count > 0)
            {
                model.InsertSeparatorAt(count);
            }
        }

        public override bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            var result = false;
            var linkUrl = parameters.LinkUrl;
            var selectionText = parameters.SelectionText;

            ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(delegate
            {
                switch (commandId)
                {
                    case CefMenuCommand.ViewSource:
                        {
                            WindowManager.ShowViewSourceWindow(chromiumWebBrowser.Address);
                            result = true;
                            break;
                        }
                    case Search:
                        {
                            WindowManager.ShowBrowser(SearchEngineHelper.GetUrl(selectionText, GlobalData.Settings.SearchEngine));
                            result = true;
                            break;
                        }
                    case OpenInNewWindow:
                        {
                            WindowManager.ShowBrowser(linkUrl);
                            result = true;
                            break;
                        }
                    case OpenSelectedUrl:
                        {
                            WindowManager.ShowBrowser(selectionText);
                            result = true;
                            break;
                        }
                }
            });
            return result;
        }

        public override void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
            ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(delegate
            {
                ((FrameworkElement)chromiumWebBrowser).ContextMenu = null;
            });
        }

        public override bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            var menuItemInfoList = CefMenuItemInfo.GetMenuItemInfoList(model);

            ((IWpfWebBrowser)chromiumWebBrowser).Dispatcher.Invoke(() =>
            {
                var menu = new ContextMenu
                {
                    IsOpen = true,
                    VerticalOffset = -8,
                    HorizontalOffset = -8,
                    ItemsSource = GetMenuItems(menuItemInfoList, callback)
                };

                void menuClosedHandler(object s, RoutedEventArgs e)
                {
                    menu.Closed -= menuClosedHandler;
                    if (!callback.IsDisposed)
                        callback.Cancel();
                }
                menu.Closed += menuClosedHandler;

                ((FrameworkElement)chromiumWebBrowser).ContextMenu = menu;
            });

            return true;
        }

        private static IEnumerable<Control> GetMenuItems(IList<CefMenuItemInfo> subMenuItemInfos, IRunContextMenuCallback callback)
        {
            if (subMenuItemInfos == null)
            {
                yield break;
            }

            foreach (var item in subMenuItemInfos)
            {
                if (item.CommandID == CefMenuCommand.NotFound)
                {
                    yield return new Separator();
                    continue;
                }

                yield return new MenuItem
                {
                    Header = GetHeader(item),
                    Padding = new Thickness(10, 5, 10, 5),
                    IsChecked = item.IsChecked,
                    ItemsSource = GetMenuItems(item.SubMenuItemInfos, callback),

                    Command = new DelegateCommand
                    {
                        CanExecute = item.IsEnable,
                        Execute = delegate { callback.Continue(item.CommandID, CefEventFlags.None); }
                    }
                };
            }
        }

        private static string GetHeader(CefMenuItemInfo menuItemInfo)
        {
            if (_menuItemHeaderKeys.TryGetValue(menuItemInfo.CommandID, out string key))
            {
                return LanguageManager.GetString(key);
            }
            else
            {
                return menuItemInfo.Header;
            }
        }

        private static readonly Dictionary<CefMenuCommand, string> _menuItemHeaderKeys = new Dictionary<CefMenuCommand, string>()
        {
            [CefMenuCommand.Back] = "browser_back",
            [CefMenuCommand.Forward] = "browser_forward",
            [CefMenuCommand.Cut] = "browser_cut",
            [CefMenuCommand.Copy] = "browser_copy",
            [CefMenuCommand.Paste] = "browser_paste",
            [CefMenuCommand.Print] = "browser_print",
            [CefMenuCommand.ViewSource] = "browser_viewSource",
            [CefMenuCommand.Undo] = "browser_undo",
            [CefMenuCommand.StopLoad] = "browser_stop",
            [CefMenuCommand.SelectAll] = "browser_selectAll",
            [CefMenuCommand.Redo] = "browser_redo",
            [CefMenuCommand.Find] = "browser_find",
            [CefMenuCommand.AddToDictionary] = "browser_addToDictionary",
            [CefMenuCommand.Reload] = "browser_reload",
            [CefMenuCommand.ReloadNoCache] = "browser_reloadNoCache",
        };
    }
}
