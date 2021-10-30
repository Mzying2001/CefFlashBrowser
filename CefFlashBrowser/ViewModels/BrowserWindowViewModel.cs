using CefFlashBrowser.Models.FlashBrowser;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using CefSharp;
using IWshRuntimeLibrary;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Diagnostics;

namespace CefFlashBrowser.ViewModels
{
    public class BrowserWindowViewModel : ViewModelBase
    {
        public Action CloseWindow { get; set; }

        public DelegateCommand OpenDevToolCommand { get; set; }
        public DelegateCommand ViewSourceCommand { get; set; }
        public DelegateCommand OpenInDefaultBrowserCommand { get; set; }
        public DelegateCommand CreateShortcutCommand { get; set; }
        public DelegateCommand CloseWindowCommand { get; set; }
        public DelegateCommand AddFavoriteCommand { get; set; }

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
            var title = Browser.Title;
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

        private void AddFavorite()
        {
            AddFavoriteDialog.Show(Browser.Title, Browser.Address);
        }

        public BrowserWindowViewModel()
        {
            OpenDevToolCommand = new DelegateCommand(() => Browser.ShowDevTools());
            ViewSourceCommand = new DelegateCommand(ViewSource);
            OpenInDefaultBrowserCommand = new DelegateCommand(OpenInDefaultBrowser);
            CreateShortcutCommand = new DelegateCommand(CreateShortcut);
            CloseWindowCommand = new DelegateCommand(() => CloseWindow?.Invoke());
            AddFavoriteCommand = new DelegateCommand(AddFavorite);
        }
    }
}
