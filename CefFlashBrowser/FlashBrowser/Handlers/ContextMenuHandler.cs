using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefFlashBrowser.Views;
using CefSharp;
using CefSharp.WinForms;
using System;
using System.Linq;

namespace CefFlashBrowser.FlashBrowser.Handlers
{
    public class ContextMenuHandler : ContextMenuHandlerBase
    {
        public const CefMenuCommand OpenInNewWindow = CefMenuCommand.UserFirst + 1;
        public const CefMenuCommand Search = CefMenuCommand.UserFirst + 2;

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
            var webBrowser = (ChromiumWebBrowser)chromiumWebBrowser;

            var linkUrl = parameters.LinkUrl;
            var selectionText = parameters.SelectionText;

            switch (commandId)
            {
                case CefMenuCommand.ViewSource:
                    {
                        webBrowser.Invoke(new Action(() =>
                        {
                            ViewSourceWindow.Show(webBrowser.Address);
                        }));
                        return true;
                    }
                case Search:
                    {
                        webBrowser.Invoke(new Action(() =>
                        {
                            BrowserWindow.Show(SearchEngineUtil.GetUrl(selectionText, GlobalData.Settings.SearchEngine));
                        }));
                        return true;
                    }
                case OpenInNewWindow:
                    {
                        webBrowser.Invoke(new Action(() =>
                        {
                            BrowserWindow.Show(linkUrl);
                        }));
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
        }

        public override bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            var menuItems = GetMenuItems(model).ToList();

            var linkUrl = parameters.LinkUrl;
            var selectionText = parameters.SelectionText;

            foreach (var item in menuItems)
            {
                switch (item.commandId)
                {
                    case CefMenuCommand.Back:
                        {
                            var header = LanguageManager.GetString("menu_back");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.Forward:
                        {
                            var header = LanguageManager.GetString("menu_forward");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.Cut:
                        {
                            var header = LanguageManager.GetString("menu_cut");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.Copy:
                        {
                            var header = LanguageManager.GetString("menu_copy");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.Paste:
                        {
                            var header = LanguageManager.GetString("menu_paste");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.Print:
                        {
                            var header = LanguageManager.GetString("menu_print");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.ViewSource:
                        {
                            var header = LanguageManager.GetString("menu_viewSource");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.Undo:
                        {
                            var header = LanguageManager.GetString("menu_undo");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.StopLoad:
                        {
                            var header = LanguageManager.GetString("menu_stop");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.SelectAll:
                        {
                            var header = LanguageManager.GetString("menu_selectAll");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.Redo:
                        {
                            var header = LanguageManager.GetString("menu_redo");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.Find:
                        {
                            var header = LanguageManager.GetString("menu_find");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.AddToDictionary:
                        {
                            var header = LanguageManager.GetString("menu_addToDictionary");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.Reload:
                        {
                            var header = LanguageManager.GetString("menu_reload");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case CefMenuCommand.ReloadNoCache:
                        {
                            var header = LanguageManager.GetString("menu_reloadNoCache");
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case OpenInNewWindow:
                        {
                            var header = string.Format(LanguageManager.GetString(item.header), linkUrl);
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    case Search:
                        {
                            var tmp = selectionText.Length > 32 ? selectionText.Substring(0, 32) + "..." : selectionText;
                            var header = string.Format(LanguageManager.GetString(item.header), tmp.Replace('\n', ' '));
                            model.SetLabelAt(item.index, header);
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            }

            return false;
        }
    }
}
