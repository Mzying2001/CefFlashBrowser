using CefFlashBrowser.Models;
using CefFlashBrowser.Views;
using CefSharp;
using CefSharp.Wpf;
using SimpleMvvm.Command;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.FlashBrowser
{
    public class BrowserWindowMenuHandler : ContextMenuHandlerBase
    {
        public const CefMenuCommand OpenInNewWindow = CefMenuCommand.UserFirst + 1;

        public override void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            if (!string.IsNullOrWhiteSpace(parameters.LinkUrl))
            {
                model.InsertCheckItemAt(0, OpenInNewWindow, "menu_openInNewWindow");
                model.InsertSeparatorAt(1);
            }
        }

        //public override bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        //{
        //    return false;
        //}

        public override void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
            var webBrowser = (ChromiumWebBrowser)chromiumWebBrowser;
            webBrowser.Dispatcher.Invoke(() =>
            {
                webBrowser.ContextMenu = null;
            });
        }

        public override bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            var webBrowser = (ChromiumWebBrowser)chromiumWebBrowser;
            var menuItems = GetMenuItems(model).ToList();

            var linkUrl = parameters.LinkUrl;

            webBrowser.Dispatcher.Invoke(() =>
            {
                var menu = new ContextMenu { IsOpen = true };

                RoutedEventHandler handler = null;
                handler = (s, e) =>
                {
                    menu.Closed -= handler;
                    if (!callback.IsDisposed)
                        callback.Cancel();
                };
                menu.Closed += handler;

                foreach (var item in menuItems)
                {
                    var header = item.header;
                    var commandId = item.commandId;
                    var isEnable = item.isEnable;

                    if (commandId == CefMenuCommand.NotFound)
                    {
                        menu.Items.Add(new Separator());
                        continue;
                    }

                    var menuItem = new MenuItem();

                    switch (commandId)
                    {
                        case CefMenuCommand.Back:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_back");
                                menuItem.Command = webBrowser.BackCommand;
                                menuItem.InputGestureText = "Alt+Z";
                                break;
                            }
                        case CefMenuCommand.Forward:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_forward");
                                menuItem.Command = webBrowser.ForwardCommand;
                                menuItem.InputGestureText = "Alt+X";
                                break;
                            }
                        case CefMenuCommand.Cut:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_cut");
                                menuItem.Command = webBrowser.CutCommand;
                                menuItem.InputGestureText = "Ctrl+X";
                                break;
                            }
                        case CefMenuCommand.Copy:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_copy");
                                menuItem.Command = webBrowser.CopyCommand;
                                menuItem.InputGestureText = "Ctrl+C";
                                break;
                            }
                        case CefMenuCommand.Paste:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_paste");
                                menuItem.Command = webBrowser.PasteCommand;
                                menuItem.InputGestureText = "Ctrl+V";
                                break;
                            }
                        case CefMenuCommand.Print:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_print");
                                menuItem.Command = webBrowser.PrintCommand;
                                menuItem.InputGestureText = "Ctrl+P";
                                break;
                            }
                        case CefMenuCommand.ViewSource:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_viewSource");
                                //menuItem.Command = webBrowser.ViewSourceCommand;
                                menuItem.Command = new DelegateCommand(() =>
                                {
                                    ViewSourceWindow.Show(webBrowser.Address);
                                });
                                menuItem.InputGestureText = "Ctrl+U";
                                break;
                            }
                        case CefMenuCommand.Undo:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_undo");
                                menuItem.Command = webBrowser.UndoCommand;
                                break;
                            }
                        case CefMenuCommand.StopLoad:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_stop");
                                menuItem.Command = webBrowser.StopCommand;
                                menuItem.InputGestureText = "Esc";
                                break;
                            }
                        case CefMenuCommand.SelectAll:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_selectAll");
                                menuItem.Command = webBrowser.SelectAllCommand;
                                menuItem.InputGestureText = "Ctrl+A";
                                break;
                            }
                        case CefMenuCommand.Redo:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_redo");
                                menuItem.Command = webBrowser.RedoCommand;
                                break;
                            }
                        //case CefMenuCommand.Find:
                        //    {
                        //        menuItem.Header = LanguageManager.GetString("menu_find");
                        //        menuItem.Command = new DelegateCommand(() =>
                        //        {
                        //            browser.GetHost().Find(0, parameters.SelectionText, true, false, false);
                        //        });
                        //        break;
                        //    }
                        //case CefMenuCommand.AddToDictionary:
                        //    {
                        //        menuItem.Header = LanguageManager.GetString("menu_addToDictionary");
                        //        menuItem.Command = new DelegateCommand(() =>
                        //        {
                        //            browser.GetHost().AddWordToDictionary(parameters.MisspelledWord);
                        //        });
                        //        break;
                        //    }
                        case CefMenuCommand.Reload:
                            {
                                menuItem.Header = LanguageManager.GetString("menu_reload");
                                menuItem.Command = webBrowser.ReloadCommand;
                                menuItem.InputGestureText = "F5";
                                break;
                            }
                        //case CefMenuCommand.ReloadNoCache:
                        //    {
                        //        menuItem.Header = LanguageManager.GetString("menu_reloadNoCache");
                        //        menuItem.Command = new DelegateCommand(() =>
                        //        {
                        //            browser.Reload(ignoreCache: true);
                        //        });
                        //        break;
                        //    }
                        case OpenInNewWindow:
                            {
                                menuItem.Header = LanguageManager.GetString(header);
                                menuItem.Command = new DelegateCommand(() =>
                                {
                                    BrowserWindow.Show(linkUrl);
                                });
                                break;
                            }
                        default:
                            {
                                menuItem.Header = header;
                                menuItem.Command = new DelegateCommand(() =>
                                {
                                    callback.Continue(commandId, CefEventFlags.None);
                                });
                                break;
                            }
                    }

                    menuItem.IsEnabled = isEnable;
                    menu.Items.Add(menuItem);
                }

                menu.Closed += (s, e) =>
                {
                    if (!callback.IsDisposed)
                        callback.Cancel();
                };

                webBrowser.ContextMenu = menu;
            });

            return true;
        }
    }
}
