using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using CefSharp;
using IWshRuntimeLibrary;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

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
        public DelegateCommand ToggleFullscreenCommand { get; set; }



        private string _address = "about:blank";
        public string Address
        {
            get => _address;
            set => UpdateValue(ref _address, value);
        }

        private IntPtr _integratedDevToolsHandle = IntPtr.Zero;
        public IntPtr IntegratedDevToolsHandle
        {
            get => _integratedDevToolsHandle;
            set => UpdateValue(ref _integratedDevToolsHandle, value);
        }

        private bool _fullscreen = false;
        public bool Fullscreen
        {
            get => _fullscreen;
            set
            {
                if (_fullscreen != value)
                {
                    UpdateValue(ref _fullscreen, value);
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

        private static string ReplaceInvalidFileNameChars(string str, char replaceChar = '_')
        {
            if (string.IsNullOrEmpty(str))
                return str;

            var builder = new StringBuilder(str);
            var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());

            if (invalidChars.Contains(replaceChar))
                replaceChar = '_';

            for (int i = 0; i < builder.Length; i++)
            {
                if (invalidChars.Contains(builder[i]))
                    builder[i] = replaceChar;
            }

            return builder.ToString();
        }

        public void CreateShortcut(IWebBrowser browser)
        {
            var title = ReplaceInvalidFileNameChars(GetWebBrowserTitle(browser));

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
                if (IntegratedDevToolsHandle == IntPtr.Zero
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

        public void ToggleFullscreen(IWebBrowser browser)
        {
            if (Fullscreen)
            {
                if (browser.CanExecuteJavascriptInMainFrame)
                    browser.ExecuteScriptAsync("if (document.fullscreenElement) document.exitFullscreen();");
                Fullscreen = false;
            }
            else
            {
                //browser.ExecuteScriptAsync("document.documentElement.requestFullscreen();");
                Fullscreen = true;
            }
        }

        public void OnNewPage(string url)
        {
            switch (GlobalData.Settings.NewPageBehavior)
            {
                case NewPageBehavior.NewWindow:
                    {
                        Fullscreen = false;
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
            Fullscreen = false;
            WindowManager.ShowPopupWebPage(url, features);
        }

        public void OnDevToolsOpened(IWebBrowser browser, IntPtr hDevTools)
        {
            if (GlobalData.Settings.EnableIntegratedDevTools)
            {
                HwndHelper.ApplyEmbeddedChildStyle(hDevTools);
                IntegratedDevToolsHandle = hDevTools;
            }
            Messenger.Global.Send(MessageTokens.DEVTOOLS_OPENED, browser);
        }

        public void OnDevToolsClosed(IWebBrowser browser)
        {
            IntegratedDevToolsHandle = IntPtr.Zero;
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
            ToggleFullscreenCommand = new DelegateCommand<IWebBrowser>(ToggleFullscreen);
        }
    }
}
