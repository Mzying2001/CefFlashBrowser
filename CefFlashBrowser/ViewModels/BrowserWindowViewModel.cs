using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
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
        public DelegateCommand OpenInSwfPlayerCommand { get; set; }

        public void ShowMainWindow()
        {
            WindowManager.ShowMainWindow();
        }

        public void ViewSource(string url)
        {
            WindowManager.ShowViewSourceWindow(url);
        }

        public void OpenInDefaultBrowser(string url)
        {
            Process.Start(url);
        }

        private static string GetWebBrowserTitle(IWebBrowser browser)
        {
            try
            {
                if (browser is WinformCefSharp4WPF.ChromiumWebBrowser b)
                {
                    return b.Title;
                }
                else
                {
                    var frame = browser.GetBrowser().MainFrame;
                    var result = frame.EvaluateScriptAsync("document.title", timeout: TimeSpan.FromSeconds(1)).Result;
                    return result.Result.ToString();
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        public void CreateShortcut(IWebBrowser browser)
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
                    WindowManager.ShowError(e.Message);
                }
            }
        }

        public void AddFavorite(IWebBrowser browser)
        {
            WindowManager.ShowAddFavoriteDialog(GetWebBrowserTitle(browser), browser.Address);
        }

        public void CloseBrowser(IWebBrowser browser)
        {
            bool forceClose = GlobalData.Settings.DisableOnBeforeUnloadDialog;
            browser.GetBrowser().CloseBrowser(forceClose);
        }

        public void ReloadOrStop(IWebBrowser browser)
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

        public void ShowDevTools(IWebBrowser browser)
        {
            browser.ShowDevTools();
        }

        public void OpenInSwfPlayer(string url)
        {
            WindowManager.ShowSwfPlayer(url);
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
            OpenInSwfPlayerCommand = new DelegateCommand<string>(OpenInSwfPlayer);
        }
    }
}
