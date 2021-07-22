using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.FlashBrowser;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefSharp;
using IWshRuntimeLibrary;

namespace CefFlashBrowser.ViewModels
{
    class BrowserWindowViewModel : NotificationObject
    {
        public Action CloseWindow { get; set; }

        public ICommand OpenDevToolCommand { get; set; }
        public ICommand ViewSourceCommand { get; set; }
        public ICommand OpenInDefaultBrowserCommand { get; set; }
        public ICommand CreateShortcutCommand { get; set; }
        public ICommand CloseWindowCommand { get; set; }

        public ChromiumFlashBrowser Browser { get; set; }

        public void SetBrowser(ChromiumFlashBrowser browser)
        {
            Browser = browser;
        }

        private void ViewSource()
        {
            ViewSourceWindow.Show(Browser.Address);
        }

        private void OpenInDefaultBrowser()
        {
            Process.Start(Browser.Address);
        }

        private void CreateShortcut()
        {
            var sfd = new Microsoft.Win32.SaveFileDialog()
            {
                FileName = Browser.Title.Replace('.', '_'),
                Filter = $"{LanguageManager.GetString("filter_shortcut")}|*.lnk",
            };

            if (sfd.ShowDialog() == true)
            {
                var path = GetType().Assembly.Location;
                var arg = Browser.Address;
                var fileName = sfd.FileName;

                try
                {
                    WshShortcut shortcut = new WshShell().CreateShortcut(fileName);
                    shortcut.TargetPath = path;
                    shortcut.Arguments = arg;
                    shortcut.Save();
                }
                catch (Exception e)
                {
                    JsAlertDialog.Show(e.Message);
                }
            }
        }

        public BrowserWindowViewModel()
        {
            OpenDevToolCommand = new DelegateCommand(() => Browser.ShowDevTools());
            ViewSourceCommand = new DelegateCommand(ViewSource);
            OpenInDefaultBrowserCommand = new DelegateCommand(OpenInDefaultBrowser);
            CreateShortcutCommand = new DelegateCommand(CreateShortcut);
            CloseWindowCommand = new DelegateCommand(() => CloseWindow?.Invoke());
        }
    }
}
