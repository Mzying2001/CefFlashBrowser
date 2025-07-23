using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefSharp;
using IWshRuntimeLibrary;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;
using System;
using System.Diagnostics;
using System.IO;

namespace CefFlashBrowser.ViewModels
{
    public class BrowserWindowViewModel : ViewModelBase
    {
        public DelegateCommand ShowMainWindowCommand { get; set; }
        public DelegateCommand ViewSourceCommand { get; set; }
        public DelegateCommand OpenInDefaultBrowserCommand { get; set; }
        public DelegateCommand CreateShortcutCommand { get; set; }
        public DelegateCommand AddFavoriteCommand { get; set; }
        public DelegateCommand ToggleLoadingStateCommand { get; set; }
        public DelegateCommand OpenInSwfPlayerCommand { get; set; }
        public DelegateCommand NewBrowserWindowCommand { get; set; }
        public DelegateCommand ToggleDevToolsCommand { get; set; }
        public DelegateCommand ToggleFullScreenCommand { get; set; }



        private string _address = "about:blank";
        public string Address
        {
            get => _address;
            set => UpdateValue(ref _address, value);
        }

        private IntPtr _devtoolsHandle = IntPtr.Zero;
        public IntPtr DevToolsHandle
        {
            get => _devtoolsHandle;
            set => UpdateValue(ref _devtoolsHandle, value);
        }

        private bool _fullScreen = false;
        public bool FullScreen
        {
            get => _fullScreen;
            set
            {
                if (_fullScreen != value)
                {
                    UpdateValue(ref _fullScreen, value);
                    Messenger.Global.Send(MessageTokens.FULLSCREEN_CHANGED, this);
                }
            }
        }



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
                if (browser is WinformCefSharp4WPF.IWpfWebBrowser b)
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
            catch (Exception e)
            {
                LogHelper.LogError("Failed to get web browser title", e);
                return string.Empty;
            }
        }

        public void CreateShortcut(IWebBrowser browser)
        {
            var title = GetWebBrowserTitle(browser);

            foreach (var item in Path.GetInvalidFileNameChars())
            {
                title = title.Replace(item, '_');
            }

            var sfd = new Microsoft.Win32.SaveFileDialog()
            {
                FileName = title,
                Filter = $"{LanguageManager.GetString("common_shortcut")}|*.lnk",
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
                    LogHelper.LogError($"Failed to create shortcut: {fileName}", e);
                    WindowManager.ShowError(e.Message);
                }
            }
        }

        public void AddFavorite(IWebBrowser browser)
        {
            WindowManager.ShowAddFavoriteDialog(GetWebBrowserTitle(browser), browser.Address);
        }

        public void ToggleLoadingState(IWebBrowser browser)
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

        public void OpenInSwfPlayer(string url)
        {
            WindowManager.ShowSwfPlayer(url);
        }

        public void NewBrowserWindow(string url)
        {
            WindowManager.ShowBrowser(url ?? "about:blank");
        }

        public void ToggleDevTools(IWebBrowser browser)
        {
            if (browser != null)
            {
                if (DevToolsHandle == IntPtr.Zero
                    && HwndHelper.FindNotIntegratedDevTools(browser) == IntPtr.Zero)
                {
                    browser.ShowDevTools();
                }
                else
                {
                    browser.CloseDevTools();
                }
            }
        }

        public void ToggleFullScreen(IWebBrowser browser)
        {
            if (FullScreen)
            {
                if (browser.CanExecuteJavascriptInMainFrame)
                    browser.ExecuteScriptAsync("if (document.fullscreenElement) document.exitFullscreen();");
                FullScreen = false;
            }
            else
            {
                //browser.ExecuteScriptAsync("document.documentElement.requestFullscreen();");
                FullScreen = true;
            }
        }

        public void OnNewPage(string url)
        {
            switch (GlobalData.Settings.NewPageBehavior)
            {
                case NewPageBehavior.NewWindow:
                    {
                        FullScreen = false;
                        WindowManager.ShowBrowser(url);
                        break;
                    }
                case NewPageBehavior.OriginalWindow:
                    {
                        Address = url;
                        break;
                    }
            }
        }

        public void OnPopup(string url, IPopupFeatures features)
        {
            FullScreen = false;
            WindowManager.ShowPopupWebPage(url, features);
        }

        public void OnDevToolsOpened(IWebBrowser browser, IntPtr hDevTools)
        {
            if (GlobalData.Settings.EnableIntegratedDevTools)
            {
                HwndHelper.SetWindowStyle(hDevTools, Win32.WS_CHILD | Win32.WS_VISIBLE);
                DevToolsHandle = hDevTools;
            }
            Messenger.Global.Send(MessageTokens.DEVTOOLS_OPENED, browser);
        }

        public void OnDevToolsClosed(IWebBrowser browser)
        {
            DevToolsHandle = IntPtr.Zero;
            Messenger.Global.Send(MessageTokens.DEVTOOLS_CLOSED, browser);
        }

        public BrowserWindowViewModel()
        {
            ShowMainWindowCommand = new DelegateCommand(ShowMainWindow);
            ViewSourceCommand = new DelegateCommand<string>(ViewSource);
            OpenInDefaultBrowserCommand = new DelegateCommand<string>(OpenInDefaultBrowser);
            CreateShortcutCommand = new DelegateCommand<IWebBrowser>(CreateShortcut);
            AddFavoriteCommand = new DelegateCommand<IWebBrowser>(AddFavorite);
            ToggleLoadingStateCommand = new DelegateCommand<IWebBrowser>(ToggleLoadingState);
            OpenInSwfPlayerCommand = new DelegateCommand<string>(OpenInSwfPlayer);
            NewBrowserWindowCommand = new DelegateCommand<string>(NewBrowserWindow);
            ToggleDevToolsCommand = new DelegateCommand<IWebBrowser>(ToggleDevTools);
            ToggleFullScreenCommand = new DelegateCommand<IWebBrowser>(ToggleFullScreen);
        }
    }
}
