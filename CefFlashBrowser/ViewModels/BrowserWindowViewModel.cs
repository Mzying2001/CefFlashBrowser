using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
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
        public DelegateCommand ShowMainWindowCommand { get; set; }
        public DelegateCommand ViewSourceCommand { get; set; }
        public DelegateCommand OpenInDefaultBrowserCommand { get; set; }
        public DelegateCommand CreateShortcutCommand { get; set; }
        public DelegateCommand AddFavoriteCommand { get; set; }
        public DelegateCommand CloseBrowserCommand { get; set; }
        public DelegateCommand ReloadOrStopCommand { get; set; }
        public DelegateCommand ShowDevToolsCommand { get; set; }

        private void ShowMainWindow()
        {
            MainWindow.Show();
        }

        private void ViewSource(string url)
        {
            ViewSourceWindow.Show(url);
        }

        private void OpenInDefaultBrowser(string url)
        {
            Process.Start(url);
        }

        private static string GetWebBrowserTitle(IWebBrowser browser)
        {
            try
            {
                var frame = browser.GetBrowser().MainFrame;
                var result = frame.EvaluateScriptAsync("document.title", timeout: TimeSpan.FromSeconds(1)).Result;
                return result.Result.ToString();
            }
            catch
            {
                return null;
            }
        }

        private void CreateShortcut(IWebBrowser browser)
        {
            var title = GetWebBrowserTitle(browser);
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
                    JsAlertDialog.ShowDialog(e.Message);
                }
            }
        }

        private void AddFavorite(IWebBrowser browser)
        {
            AddFavoriteDialog.ShowDialog(GetWebBrowserTitle(browser), browser.Address);
        }

        private void CloseBrowser(IWebBrowser browser)
        {
            bool forceClose = GlobalData.Settings.DisableOnBeforeUnloadDialog;
            browser.GetBrowser().CloseBrowser(forceClose);
        }

        private void ReloadOrStop(IWebBrowser browser)
        {
            if (browser.IsLoading)
            {
                browser.Stop();
            }
            else
            {
                browser.Reload();
            }
        }

        private void ShowDevTools(IWebBrowser browser)
        {
            browser.ShowDevTools();
        }

        public BrowserWindowViewModel()
        {
            ShowMainWindowCommand = new DelegateCommand(ShowMainWindow);
            ViewSourceCommand = new DelegateCommand<string>(ViewSource);
            OpenInDefaultBrowserCommand = new DelegateCommand<string>(OpenInDefaultBrowser);
            CreateShortcutCommand = new DelegateCommand<IWebBrowser>(CreateShortcut);
            AddFavoriteCommand = new DelegateCommand<IWebBrowser>(AddFavorite);
            CloseBrowserCommand = new DelegateCommand<IWebBrowser>(CloseBrowser);
            ReloadOrStopCommand = new DelegateCommand<IWebBrowser>(ReloadOrStop);
            ShowDevToolsCommand = new DelegateCommand<IWebBrowser>(ShowDevTools);
        }
    }
}
