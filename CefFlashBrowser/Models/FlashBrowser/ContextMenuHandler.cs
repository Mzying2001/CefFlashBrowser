using CefFlashBrowser.Commands;
using CefFlashBrowser.Models.StaticData;
using CefSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Models.FlashBrowser
{
    class ContextMenuHandler : IContextMenuHandler
    {
        public void OnBeforeContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model)
        {
            //throw new NotImplementedException();
        }

        public bool OnContextMenuCommand(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, CefMenuCommand commandId, CefEventFlags eventFlags)
        {
            return true;
        }

        public void OnContextMenuDismissed(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame)
        {
            var targetBrowser = (ChromiumFlashBrowser)chromiumWebBrowser;
            targetBrowser.Dispatcher.Invoke(() =>
            {
                targetBrowser.ContextMenu = null;
            });
        }

        public bool RunContextMenu(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame, IContextMenuParams parameters, IMenuModel model, IRunContextMenuCallback callback)
        {
            var targetBrowser = (ChromiumFlashBrowser)chromiumWebBrowser;

            targetBrowser.Dispatcher.Invoke(() =>
            {
                var menu = new ContextMenu { IsOpen = true };

                menu.Closed += (s, e) =>
                {
                    if (!callback.IsDisposed)
                        callback.Cancel();
                };

                //menu.Items.Add(new MenuItem
                //{
                //    Header = LanguageManager.GetString("menu_back"),
                //    InputGestureText = "Alt+Z",
                //    Command = targetBrowser.BackCommand
                //});

                //menu.Items.Add(new MenuItem
                //{
                //    Header = LanguageManager.GetString("menu_forward"),
                //    InputGestureText = "Alt+X",
                //    Command = targetBrowser.ForwardCommand
                //});

                //if (targetBrowser.ReloadCommand.CanExecute(null))
                //{
                //    menu.Items.Add(new MenuItem
                //    {
                //        Header = LanguageManager.GetString("menu_reload"),
                //        InputGestureText = "F5",
                //        Command = targetBrowser.ReloadCommand
                //    });
                //}
                //else if (targetBrowser.StopCommand.CanExecute(null))
                //{
                //    menu.Items.Add(new MenuItem
                //    {
                //        Header = LanguageManager.GetString("menu_stop"),
                //        InputGestureText = "Esc",
                //        Command = targetBrowser.StopCommand
                //    });
                //}

                //menu.Items.Add(new Separator());

                menu.Items.Add(new MenuItem
                {
                    Header = LanguageManager.GetString("menu_cut"),
                    InputGestureText = "Ctrl+X",
                    Command = targetBrowser.CutCommand
                });

                menu.Items.Add(new MenuItem
                {
                    Header = LanguageManager.GetString("menu_copy"),
                    InputGestureText = "Ctrl+C",
                    Command = targetBrowser.CopyCommand
                });

                menu.Items.Add(new MenuItem
                {
                    Header = LanguageManager.GetString("menu_paste"),
                    InputGestureText = "Ctrl+V",
                    Command = targetBrowser.PasteCommand
                });

                menu.Items.Add(new MenuItem
                {
                    Header = LanguageManager.GetString("menu_selectAll"),
                    InputGestureText = "Ctrl+A",
                    Command = targetBrowser.SelectAllCommand
                });

                menu.Items.Add(new Separator());

                menu.Items.Add(new MenuItem
                {
                    Header = LanguageManager.GetString("menu_print"),
                    InputGestureText = "Ctrl+P",
                    Command = targetBrowser.PrintCommand
                });

                targetBrowser.ContextMenu = menu;
            });

            return true;
        }
    }
}
