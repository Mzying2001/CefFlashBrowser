using CefFlashBrowser.Commands;
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
    class MenuHandler : IContextMenuHandler
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

                menu.Items.Add(new MenuItem
                {
                    Header = "back",
                    Command = targetBrowser.BackCommand
                });

                menu.Items.Add(new MenuItem
                {
                    Header = "forward",
                    Command = targetBrowser.ForwardCommand
                });

                if (targetBrowser.ReloadCommand.CanExecute(null))
                {
                    menu.Items.Add(new MenuItem
                    {
                        Header = "reload",
                        Command = targetBrowser.ReloadCommand
                    });
                }
                else if (targetBrowser.StopCommand.CanExecute(null))
                {
                    menu.Items.Add(new MenuItem
                    {
                        Header = "stop",
                        Command = targetBrowser.StopCommand
                    });
                }

                menu.Items.Add(new Separator());

                menu.Items.Add(new MenuItem
                {
                    Header = "cut",
                    Command = targetBrowser.CutCommand
                });

                menu.Items.Add(new MenuItem
                {
                    Header = "copy",
                    Command = targetBrowser.CopyCommand
                });

                menu.Items.Add(new MenuItem
                {
                    Header = "paste",
                    Command = targetBrowser.PasteCommand
                });

                menu.Items.Add(new MenuItem
                {
                    Header = "select all",
                    Command = targetBrowser.SelectAllCommand
                });

                menu.Items.Add(new Separator());

                menu.Items.Add(new MenuItem
                {
                    Header = "print",
                    Command = targetBrowser.PrintCommand
                });

                targetBrowser.ContextMenu = menu;
            });

            return true;
        }
    }
}
