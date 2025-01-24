using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;
using System;

namespace CefFlashBrowser.ViewModels
{
    public class MainWindowViewModel : ViewModelBase, IDisposable
    {
        private bool _disposed;

        public DelegateCommand OpenUrlCommand { get; set; }
        public DelegateCommand OpenSettingsWindowCommand { get; set; }
        public DelegateCommand OpenFavoritesManagerCommand { get; set; }
        public DelegateCommand LoadSwfCommand { get; set; }
        public DelegateCommand ViewGithubCommand { get; set; }
        public DelegateCommand OpenWebsiteCommand { get; set; }
        public DelegateCommand DropFileCommand { get; set; }

        public string WelcomeText
        {
            get => string.Format("{0} {1}",
                LanguageManager.GetString("window_title"), EmoticonsHelper.GetNextEmoticon());
        }

        private void ShowBrowser(string address)
        {
            WindowManager.ShowBrowser(address);
            if (GlobalData.Settings.HideMainWindowOnBrowsing)
                Messenger.Global.Send(MessageTokens.CLOSE_MAINWINDOW, null);
        }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                WindowManager.Alert(LanguageManager.GetString("message_emptyUrl"));
                return;
            }

            url = url.Trim();

            if (UrlHelper.IsLocalSwfFile(url))
            {
                WindowManager.ShowSwfPlayer(url);
                return;
            }

            switch (GlobalData.Settings.NavigationType)
            {
                case NavigationType.Automatic:
                    {
                        if (!UrlHelper.IsHttpUrl(url))
                            url = SearchEngineHelper.GetUrl(url, GlobalData.Settings.SearchEngine);
                    }
                    break;

                case NavigationType.SearchOnly:
                    {
                        url = SearchEngineHelper.GetUrl(url, GlobalData.Settings.SearchEngine);
                    }
                    break;

                case NavigationType.NavigateOnly:
                    {
                        //nothing to do
                    }
                    break;
            }

            ShowBrowser(url);
        }

        private void OpenSettingsWindow()
        {
            WindowManager.ShowSettingsWindow();
        }

        private void OpenFavoritesManager()
        {
            WindowManager.ShowFavoritesManager();
        }

        private void LoadSwf()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = $"{LanguageManager.GetString("filter_swf")}|*.swf"
            };
            if (ofd.ShowDialog() == true)
            {
                WindowManager.ShowSwfPlayer(ofd.FileName);
            }
        }

        private void ViewGithub()
        {
            ShowBrowser("https://github.com/Mzying2001/CefFlashBrowser");
        }

        private void OpenWebsite(Website website)
        {
            ShowBrowser(website.Url);
        }

        private void DropFile(string[] files)
        {
            foreach (var item in files)
            {
                if (UrlHelper.IsLocalSwfFile(item))
                {
                    WindowManager.ShowSwfPlayer(item);
                }
                else
                {
                    ShowBrowser(item);
                }
            }
        }

        private void OnLanguageChanged(object _)
        {
            RaisePropertyChanged(nameof(WelcomeText));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                { }
                Messenger.Global.Unregister(MessageTokens.LANGUAGE_CHANGED, OnLanguageChanged);
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        public MainWindowViewModel()
        {
            OpenUrlCommand = new DelegateCommand<string>(OpenUrl);
            OpenSettingsWindowCommand = new DelegateCommand(OpenSettingsWindow);
            OpenFavoritesManagerCommand = new DelegateCommand(OpenFavoritesManager);
            LoadSwfCommand = new DelegateCommand(LoadSwf);
            ViewGithubCommand = new DelegateCommand(ViewGithub);
            OpenWebsiteCommand = new DelegateCommand<Website>(OpenWebsite);
            DropFileCommand = new DelegateCommand<string[]>(DropFile);

            Messenger.Global.Register(MessageTokens.LANGUAGE_CHANGED, OnLanguageChanged);
        }

        ~MainWindowViewModel()
        {
            Dispose(disposing: false);
        }
    }
}
