using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefSharp;
using CefSharp.Wpf;
using IWshRuntimeLibrary;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;
using System;
using System.Diagnostics;

namespace CefFlashBrowser.ViewModels
{
    public class BrowserWindowViewModel : ViewModelBase
    {
        public DelegateCommand OpenDevToolCommand { get; set; }
        public DelegateCommand ViewSourceCommand { get; set; }
        public DelegateCommand OpenInDefaultBrowserCommand { get; set; }
        public DelegateCommand CreateShortcutCommand { get; set; }
        public DelegateCommand AddFavoriteCommand { get; set; }
        public DelegateCommand ExitBrowserCommand { get; set; }

        private void ViewSource(string url)
        {
            ViewSourceWindow.Show(url);
        }

        private void OpenInDefaultBrowser(string url)
        {
            Process.Start(url);
        }

        private void CreateShortcut(ChromiumWebBrowser browser)
        {
            var title = browser.Title;
            foreach (var item in "\\/:*?\"<>|.")
                title = title.Replace(item, '_');

            var sfd = new Microsoft.Win32.SaveFileDialog()
            {
                FileName = title,
                Filter = $"{LanguageManager.GetString("filter_shortcut")}|*.lnk",
            };

            if (sfd.ShowDialog() == true)
            {
                var path = GetType().Assembly.Location;
                var arg = browser.Address;
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

        private void AddFavorite(ChromiumWebBrowser browser)
        {
            AddFavoriteDialog.Show(browser.Title, browser.Address);
        }

        private void ExitBrowser()
        {
            Messenger.Global.Send(MessageTokens.EXIT_BROWSER, null);
        }

        public BrowserWindowViewModel()
        {
            OpenDevToolCommand = new DelegateCommand<ChromiumWebBrowser>(b => b.ShowDevTools());
            ViewSourceCommand = new DelegateCommand<string>(ViewSource);
            OpenInDefaultBrowserCommand = new DelegateCommand<string>(OpenInDefaultBrowser);
            CreateShortcutCommand = new DelegateCommand<ChromiumWebBrowser>(CreateShortcut);
            AddFavoriteCommand = new DelegateCommand<ChromiumWebBrowser>(AddFavorite);
            ExitBrowserCommand = new DelegateCommand(ExitBrowser);
        }
    }
}
