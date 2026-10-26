using CefFlashBrowser.Data;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using CefFlashBrowser.Utils;
using CefSharp;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Ioc;
using SimpleMvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
        public DelegateCommand AddBrowserCommand { get; set; }
        public DelegateCommand CloseBrowserCommand { get; set; }
        public DelegateCommand CloseOtherBrowsersCommand { get; set; }
        public DelegateCommand DuplicateBrowserCommand { get; set; }
        public DelegateCommand SelectNextBrowserCommand { get; set; }
        public DelegateCommand SelectPreviousBrowserCommand { get; set; }
        public DelegateCommand ToggleDevToolsCommand { get; set; }
        public DelegateCommand ToggleFullscreenCommand { get; set; }
        public DelegateCommand OpenFindPopupCommand { get; set; }
        public DelegateCommand CloseFindPopupCommand { get; set; }
        public DelegateCommand FindNextTextCommand { get; set; }
        public DelegateCommand FindPrevTextCommand { get; set; }



        private readonly ObservableCollection<BrowserViewModel> _browsers;
        public ReadOnlyObservableCollection<BrowserViewModel> Browsers { get; }

        private bool _isClosing;
        public bool IsClosing
        {
            get => _isClosing;
            internal set => UpdateValue(ref _isClosing, value);
        }

        private BrowserViewModel _currentBrowser;
        public BrowserViewModel CurrentBrowser
        {
            get => _currentBrowser;
            set
            {
                if (ReferenceEquals(_currentBrowser, value))
                    return;

                if (value == null ? _browsers.Count != 0 : !_browsers.Contains(value))
                    throw new ArgumentException("The current browser must belong to this window.", nameof(value));

                _currentBrowser?.StopFinding(true);
                _currentBrowser?.ExitFullScreen();
                ShowFindPopup = false;
                Fullscreen = false;
                UpdateValue(ref _currentBrowser, value);
            }
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

        public bool ShowTabStrip
        {
            get => GlobalData.Settings.AlwaysShowTabStrip || Browsers.Count > 1;
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

        private static bool IsBrowserReady(BrowserViewModel browser)
        {
            return browser?.IsBrowserReady() ?? false;
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

        public void CreateShortcut(BrowserViewModel browser)
        {
            if (!IsBrowserReady(browser))
                return;

            var title = ReplaceInvalidFileNameChars(browser.Title);

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
                    ShortcutHelper.Create(fileName, path, arg);
                }
                catch (Exception e)
                {
                    LogHelper.LogError($"Failed to create shortcut: {fileName}", e);
                    WindowManager.ShowError(e.Message);
                }
            }
        }

        public void AddFavorite(BrowserViewModel browser)
        {
            if (!IsBrowserReady(browser))
                return;

            var vm = new AddFavoriteDialogViewModel
            {
                Name = browser.Title,
                Url = browser.Address,
            };

            if (WindowManager.ShowAddFavoriteDialog(vm))
            {
                GlobalData.Favorites.Add(vm.Website.Clone());
            }
        }

        public void ToggleLoadingState(BrowserViewModel browser)
        {
            if (!IsBrowserReady(browser))
                return;

            if (browser.Browser.IsLoading)
            {
                browser.Browser.Stop();
            }
            else
            {
                browser.Browser.Reload();
            }
        }

        public void OpenInSwfPlayer(string url)
        {
            WindowManager.ShowSwfPlayer(url);
        }

        public void NewBrowserWindow(string url)
        {
            if (!IsClosing)
            {
                WindowManager.ShowBrowser(string.IsNullOrEmpty(url) ? "about:blank" : url);
            }
        }

        private BrowserViewModel InsertBrowser(int index, string address, bool activate)
        {
            if (IsClosing)
            {
                throw new InvalidOperationException(
                    "Cannot add a browser while the window is closing.");
            }

            var browser = SimpleIoc.Global.GetInstance<BrowserViewModel>(address);
            _browsers.Insert(index, browser);

            if (activate || CurrentBrowser == null)
            {
                CurrentBrowser = browser;
            }

            return browser;
        }

        public BrowserViewModel AddBrowser(string address = "about:blank", bool activate = true)
        {
            return InsertBrowser(_browsers.Count, address, activate);
        }

        private void AddBrowserCommandImpl(string address)
        {
            if (!IsClosing)
            {
                AddBrowser(address ?? GlobalData.Settings.NewTabAddress);
            }
        }

        public void CloseBrowser(BrowserViewModel browser)
        {
            if (!IsClosing && browser != null && _browsers.Contains(browser))
            {
                Messenger.Global.Send(MessageTokens.CLOSE_BROWSER, browser);
            }
        }

        public void CloseOtherBrowsers(BrowserViewModel browser)
        {
            if (!IsClosing && browser != null && _browsers.Contains(browser) && _browsers.Count > 1)
            {
                Messenger.Global.Send(MessageTokens.CLOSE_OTHER_BROWSERS, browser);
            }
        }

        public void DuplicateBrowser(BrowserViewModel browser)
        {
            if (IsClosing || browser == null || !_browsers.Contains(browser))
                return;

            InsertBrowser(_browsers.IndexOf(browser) + 1,
                string.IsNullOrEmpty(browser.Address) ? "about:blank" : browser.Address, true);
        }

        public void MoveBrowser(BrowserViewModel browser, int index)
        {
            if (IsClosing || browser == null || index < 0 || index >= _browsers.Count)
                return;

            var oldIndex = _browsers.IndexOf(browser);
            if (oldIndex >= 0 && oldIndex != index)
            {
                _browsers.Move(oldIndex, index);
            }
        }

        public void SelectBrowser(int offset)
        {
            if (IsClosing || _browsers.Count == 0)
                return;

            var index = (_browsers.IndexOf(CurrentBrowser) + offset) % _browsers.Count;

            if (index < 0)
                index += _browsers.Count;

            CurrentBrowser = _browsers[index];
        }

        internal void RemoveBrowser(BrowserViewModel browser)
        {
            var index = _browsers.IndexOf(browser);
            if (index < 0)
                return;

            bool wasCurrent = ReferenceEquals(CurrentBrowser, browser);
            _browsers.RemoveAt(index);

            if (wasCurrent)
            {
                CurrentBrowser = _browsers.Count == 0 ? null : _browsers[Math.Min(index, _browsers.Count - 1)];
            }
        }

        public void ToggleDevTools(BrowserViewModel browser)
        {
            if (IsBrowserReady(browser))
            {
                browser.ToggleDevTools();
            }
        }

        public void ToggleFullscreen(BrowserViewModel browser)
        {
            if (!IsBrowserReady(browser))
                return;

            if (!Fullscreen)
            {
                Fullscreen = true;
            }
            else
            {
                browser.ExitFullScreen();
                Fullscreen = false;
            }
        }

        public void OnNewPage(BrowserViewModel source, string url, bool activate = true, NewPageBehavior? behaviorOverride = null)
        {
            if (IsClosing || source == null || !_browsers.Contains(source))
                return;

            switch (behaviorOverride ?? GlobalData.Settings.NewPageBehavior)
            {
                case NewPageBehavior.NewTab:
                    {
                        InsertBrowser(_browsers.IndexOf(source) + 1, url, activate);
                        break;
                    }
                case NewPageBehavior.NewWindow:
                    {
                        if (source == CurrentBrowser)
                            Fullscreen = false;
                        WindowManager.ShowBrowser(url);
                        break;
                    }
                case NewPageBehavior.OriginalWindow:
                    {
                        source.Address = url;
                        break;
                    }
            }
        }

        public void OnPopup(BrowserViewModel source, string url, WindowSizeInfo sizeInfo)
        {
            if (IsClosing || source == null || !_browsers.Contains(source))
                return;

            if (source == CurrentBrowser)
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

        public void FindText(BrowserViewModel browser, bool forward)
        {
            if (!IsBrowserReady(browser))
                return;

            if (string.IsNullOrEmpty(TextToFind))
            {
                browser.StopFinding(true);
            }
            else
            {
                browser.FindText(TextToFind, forward, false, false);
            }
        }

        public void RefreshTabStripVisibility()
        {
            RaisePropertyChanged(nameof(ShowTabStrip));
        }

        private void OnBrowserCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Reset:
                    RefreshTabStripVisibility();
                    break;
            }
        }

        public BrowserWindowViewModel()
        {
            _browsers = new ObservableCollection<BrowserViewModel>();
            _browsers.CollectionChanged += OnBrowserCollectionChanged;

            Browsers = new ReadOnlyObservableCollection<BrowserViewModel>(_browsers);
            AddBrowser(null);

            ShowMainWindowCommand = new DelegateCommand(ShowMainWindow);
            ViewSourceCommand = new DelegateCommand<string>(ViewSource);
            OpenInDefaultBrowserCommand = new DelegateCommand<string>(OpenInDefaultBrowser);
            CreateShortcutCommand = new DelegateCommand<BrowserViewModel>(CreateShortcut);
            AddFavoriteCommand = new DelegateCommand<BrowserViewModel>(AddFavorite);
            ToggleLoadingStateCommand = new DelegateCommand<BrowserViewModel>(ToggleLoadingState);
            OpenInSwfPlayerCommand = new DelegateCommand<string>(OpenInSwfPlayer);
            NewBrowserWindowCommand = new DelegateCommand<string>(NewBrowserWindow);
            AddBrowserCommand = new DelegateCommand<string>(AddBrowserCommandImpl);
            CloseBrowserCommand = new DelegateCommand<BrowserViewModel>(CloseBrowser);
            CloseOtherBrowsersCommand = new DelegateCommand<BrowserViewModel>(CloseOtherBrowsers);
            DuplicateBrowserCommand = new DelegateCommand<BrowserViewModel>(DuplicateBrowser);
            SelectNextBrowserCommand = new DelegateCommand(() => SelectBrowser(1));
            SelectPreviousBrowserCommand = new DelegateCommand(() => SelectBrowser(-1));
            ToggleDevToolsCommand = new DelegateCommand<BrowserViewModel>(ToggleDevTools);
            ToggleFullscreenCommand = new DelegateCommand<BrowserViewModel>(ToggleFullscreen);
            OpenFindPopupCommand = new DelegateCommand(OpenFindPopup);
            CloseFindPopupCommand = new DelegateCommand(() => ShowFindPopup = false);
            FindNextTextCommand = new DelegateCommand<BrowserViewModel>(b => FindText(b, true));
            FindPrevTextCommand = new DelegateCommand<BrowserViewModel>(b => FindText(b, false));
        }
    }
}
