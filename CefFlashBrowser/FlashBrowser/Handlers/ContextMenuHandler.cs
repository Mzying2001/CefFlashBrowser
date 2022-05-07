using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Command;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class ContextMenuHandler : ContextMenuHandlerBase
    {
        public const CefMenuCommand OpenInNewWindow = CefMenuCommand.UserFirst + 1;
        public const CefMenuCommand Search = CefMenuCommand.UserFirst + 2;

        private readonly ChromiumWebBrowser webBrowser;

        public ContextMenuHandler(ChromiumWebBrowser chromiumWebBrowser)
        {
            webBrowser = chromiumWebBrowser;
        }

        public override void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            int count = 0;

            if (!string.IsNullOrWhiteSpace(parameters.SelectionText))
            {
                var selectionText = parameters.SelectionText;
                var header = LanguageManager.GetString("menu_search");
                var tmp = selectionText.Length > 32 ? selectionText.Substring(0, 32) + "..." : selectionText;
                header = string.Format(header, tmp.Replace('\n', ' '));
                model.InsertItemAt(0, Search, header);
                count++;
            }
            if (!string.IsNullOrWhiteSpace(parameters.LinkUrl))
            {
                var header = LanguageManager.GetString("menu_openInNewWindow");
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
            var linkUrl = parameters.LinkUrl;
            var selectionText = parameters.SelectionText;

            switch (commandId)
            {
                case CefMenuCommand.ViewSource:
                    {
                        webBrowser.Dispatcher.Invoke(delegate
                        {
                            ViewSourceWindow.Show(webBrowser.Address);
                        });
                        return true;
                    }
                case Search:
                    {
                        webBrowser.Dispatcher.Invoke(delegate
                        {
                            BrowserWindow.Show(SearchEngineUtil.GetUrl(selectionText, GlobalData.Settings.SearchEngine));
                        });
                        return true;
                    }
                case OpenInNewWindow:
                    {
                        webBrowser.Dispatcher.Invoke(delegate
                        {
                            BrowserWindow.Show(linkUrl);
                        });
                        return true;
                    }
                default:
                    {
                        return false;
                    }
            }
        }

        public override void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
            webBrowser.Dispatcher.Invoke(() =>
            {
                webBrowser.ContextMenu = null;
            });
        }

        public override bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            var menuItemInfoList = GetMenuItemInfoList(model);

            webBrowser.Dispatcher.Invoke(() =>
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

                webBrowser.ContextMenu = menu;
            });

            return true;
        }

        private IEnumerable<Control> GetMenuItems(IList<CefMenuItemInfo> subMenuItemInfos, IRunContextMenuCallback callback)
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
                    IsChecked = item.IsChecked,
                    Command = new DelegateCommand
                    {
                        CanExecute = item.IsEnable,
                        Execute = delegate { callback.Continue(item.CommandID, CefEventFlags.None); }
                    },
                    ItemsSource = GetMenuItems(item.SubMenuItemInfos, callback)
                };
            }
        }

        private string GetHeader(CefMenuItemInfo menuItemInfo)
        {
            string header;
            switch (menuItemInfo.CommandID)
            {
                case CefMenuCommand.Back:
                    {
                        header = LanguageManager.GetString("menu_back");
                        break;
                    }
                case CefMenuCommand.Forward:
                    {
                        header = LanguageManager.GetString("menu_forward");
                        break;
                    }
                case CefMenuCommand.Cut:
                    {
                        header = LanguageManager.GetString("menu_cut");
                        break;
                    }
                case CefMenuCommand.Copy:
                    {
                        header = LanguageManager.GetString("menu_copy");
                        break;
                    }
                case CefMenuCommand.Paste:
                    {
                        header = LanguageManager.GetString("menu_paste");
                        break;
                    }
                case CefMenuCommand.Print:
                    {
                        header = LanguageManager.GetString("menu_print");
                        break;
                    }
                case CefMenuCommand.ViewSource:
                    {
                        header = LanguageManager.GetString("menu_viewSource");
                        break;
                    }
                case CefMenuCommand.Undo:
                    {
                        header = LanguageManager.GetString("menu_undo");
                        break;
                    }
                case CefMenuCommand.StopLoad:
                    {
                        header = LanguageManager.GetString("menu_stop");
                        break;
                    }
                case CefMenuCommand.SelectAll:
                    {
                        header = LanguageManager.GetString("menu_selectAll");
                        break;
                    }
                case CefMenuCommand.Redo:
                    {
                        header = LanguageManager.GetString("menu_redo");
                        break;
                    }
                case CefMenuCommand.Find:
                    {
                        header = LanguageManager.GetString("menu_find");
                        break;
                    }
                case CefMenuCommand.AddToDictionary:
                    {
                        header = LanguageManager.GetString("menu_addToDictionary");
                        break;
                    }
                case CefMenuCommand.Reload:
                    {
                        header = LanguageManager.GetString("menu_reload");
                        break;
                    }
                case CefMenuCommand.ReloadNoCache:
                    {
                        header = LanguageManager.GetString("menu_reloadNoCache");
                        break;
                    }
                default:
                    {
                        header = menuItemInfo.Header;
                        break;
                    }
            }

            return header;
        }
    }
}
