using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
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
using System.Threading.Tasks;

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
        public DelegateCommand OpenFindPopupCommand { get; set; }
        public DelegateCommand CloseFindPopupCommand { get; set; }
        public DelegateCommand FindNextTextCommand { get; set; }
        public DelegateCommand FindPrevTextCommand { get; set; }



        private BrowserViewModel _currentBrowser;
        public BrowserViewModel CurrentBrowser
        {
            get => _currentBrowser;
            set => UpdateValue(ref _currentBrowser, value);
        }

        private bool _showFindPopup = false;
        public bool ShowFindPopup
        {
            get => _showFindPopup;
            set => UpdateValue(ref _showFindPopup, value);
        }

        private string _textToFind = string.Empty;
        public string TextToFind
        {
            get => _textToFind;
            set => UpdateValue(ref _textToFind, value);
        }

        private bool _fullscreen = false;
        public bool Fullscreen
        {
            get => _fullscreen;
            set
            {
                if (UpdateValue(ref _fullscreen, value))
                {
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
            try
            {
                Process.Start(url);
            }
            catch (Exception e)
            {
                LogHelper.LogError($"Failed to open URL in default browser: {url}", e);
                WindowManager.ShowError(e.Message);
            }
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
                    var frame = browser.GetBrowser()?.MainFrame;

                    if (frame == null)
                        return string.Empty;

                    // Run the awaited call on a thread-pool thread so that the
                    // blocking wait below cannot deadlock if EvaluateScriptAsync's
                    // continuation ever needs to capture the caller's
                    // SynchronizationContext. This method is invoked from the UI
                    // thread (CreateShortcut / AddFavorite), so a naked .Result
                    // here would in the worst case freeze the UI until the 1-second
                    // timeout elapses.
                    var result = Task.Run(() =>
                        frame.EvaluateScriptAsync("document.title", timeout: TimeSpan.FromSeconds(1))
                    ).GetAwaiter().GetResult();
                    return result?.Result?.ToString() ?? string.Empty;
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
            if (browser == null || browser.IsDisposed || !browser.IsBrowserInitialized)
                return;

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
            if (browser == null || browser.IsDisposed || !browser.IsBrowserInitialized)
                return;

            var vm = new AddFavoriteDialogViewModel
            {
                Name = GetWebBrowserTitle(browser),
                Url = browser.Address,
            };

            if (WindowManager.ShowAddFavoriteDialog(vm))
            {
                GlobalData.Favorites.Add(vm.Website.Clone());
            }
        }

        public void ToggleLoadingState(IWebBrowser browser)
        {
            if (browser == null || browser.IsDisposed || !browser.IsBrowserInitialized)
                return;

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

        public void ToggleDevTools(BrowserViewModel browser)
        {
            if (browser != null)
            {
                browser.ToggleDevTools();
            }
        }

        public void ToggleFullscreen(BrowserViewModel browser)
        {
            if (browser?.Browser == null || browser.Browser.IsDisposed || !browser.Browser.IsBrowserInitialized)
                return;

            if (Fullscreen)
            {
                browser.ExitFullScreen();
                Fullscreen = false;
            }
            else
            {
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
                        CurrentBrowser.Address = url;
                        break;
                    }
            }
        }

        public void OnPopup(string url, WindowSizeInfo sizeInfo)
        {
            Fullscreen = false;
            WindowManager.ShowPopupWebPage(url, sizeInfo);
        }

        private void OpenFindPopup()
        {
            if (!ShowFindPopup)
            {
                ShowFindPopup = true;
            }
            else
            {
                Messenger.Global.Send(MessageTokens.FOCUS_FIND_POPUP, this);
            }
        }

        public void FindText(IWebBrowser browser, bool forward)
        {
            if (browser == null || browser.IsDisposed || !browser.IsBrowserInitialized)
                return;

            if (string.IsNullOrEmpty(TextToFind))
            {
                browser.GetBrowser()?.StopFinding(true);
            }
            else
            {
                browser.GetBrowser()?.Find(0, TextToFind, forward, false, false);
            }
        }

        public BrowserWindowViewModel()
        {
            CurrentBrowser = new BrowserViewModel();

            ShowMainWindowCommand = new DelegateCommand(ShowMainWindow);
            ViewSourceCommand = new DelegateCommand<string>(ViewSource);
            OpenInDefaultBrowserCommand = new DelegateCommand<string>(OpenInDefaultBrowser);
            CreateShortcutCommand = new DelegateCommand<IWebBrowser>(CreateShortcut);
            AddFavoriteCommand = new DelegateCommand<IWebBrowser>(AddFavorite);
            ToggleLoadingStateCommand = new DelegateCommand<IWebBrowser>(ToggleLoadingState);
            OpenInSwfPlayerCommand = new DelegateCommand<string>(OpenInSwfPlayer);
            NewBrowserWindowCommand = new DelegateCommand<string>(NewBrowserWindow);
            ToggleDevToolsCommand = new DelegateCommand<BrowserViewModel>(ToggleDevTools);
            ToggleFullscreenCommand = new DelegateCommand<BrowserViewModel>(ToggleFullscreen);
            OpenFindPopupCommand = new DelegateCommand(OpenFindPopup);
            CloseFindPopupCommand = new DelegateCommand(() => ShowFindPopup = false);
            FindNextTextCommand = new DelegateCommand<IWebBrowser>(b => FindText(b, true));
            FindPrevTextCommand = new DelegateCommand<IWebBrowser>(b => FindText(b, false));
        }
    }
}
