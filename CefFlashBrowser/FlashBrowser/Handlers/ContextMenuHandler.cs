using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views;
using CefFlashBrowser.WinformCefSharp4WPF;
using CefSharp;
using SimpleMvvm.Command;
using System.Linq;
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
                model.InsertItemAt(0, Search, "menu_search");
                count++;
            }
            if (!string.IsNullOrWhiteSpace(parameters.LinkUrl))
            {
                model.InsertCheckItemAt(0, OpenInNewWindow, "menu_openInNewWindow");
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
            var menuItems = GetMenuItems(model).ToList();

            var linkUrl = parameters.LinkUrl;
            var selectionText = parameters.SelectionText;

            webBrowser.Dispatcher.Invoke(() =>
            {
                var menu = new ContextMenu
                {
                    IsOpen = true,
                    VerticalOffset = -8,
                    HorizontalOffset = -8,
                };

                RoutedEventHandler handler = null;
                handler = (object s, RoutedEventArgs e) =>
                {
                    menu.Closed -= handler;
                    if (!callback.IsDisposed)
                        callback.Cancel();
                };
                menu.Closed += handler;

                foreach (var item in menuItems)
                {
                    string header = string.Empty;

                    if (item.commandId == CefMenuCommand.NotFound)
                    {
                        menu.Items.Add(new Separator());
                        continue;
                    }

                    switch (item.commandId)
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
                        case OpenInNewWindow:
                            {
                                header = string.Format(LanguageManager.GetString(item.header), linkUrl);
                                break;
                            }
                        case Search:
                            {
                                var tmp = selectionText.Length > 32 ? selectionText.Substring(0, 32) + "..." : selectionText;
                                header = string.Format(LanguageManager.GetString(item.header), tmp.Replace('\n', ' '));
                                break;
                            }
                        default:
                            {
                                header = item.header;
                                break;
                            }
                    }

                    menu.Items.Add(new MenuItem
                    {
                        Header = header,
                        Command = new DelegateCommand
                        {
                            CanExecute = item.isEnable,
                            Execute = obj => callback.Continue(item.commandId, CefEventFlags.None)
                        }
                    });
                }

                webBrowser.ContextMenu = menu;
            });

            return true;
        }
    }
}
