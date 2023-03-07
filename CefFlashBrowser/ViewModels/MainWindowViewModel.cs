using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;

namespace CefFlashBrowser.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public DelegateCommand OpenUrlCommand { get; set; }
        public DelegateCommand OpenSettingsWindowCommand { get; set; }
        public DelegateCommand OpenFavoritesManagerCommand { get; set; }
        public DelegateCommand LoadSwfCommand { get; set; }
        public DelegateCommand ViewGithubCommand { get; set; }
        public DelegateCommand OpenWebsiteCommand { get; set; }
        public DelegateCommand DropFileCommand { get; set; }

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

            if (UrlChecker.IsLocalSwfFile(url))
            {
                WindowManager.ShowSwfPlayer(url);
                return;
            }

            switch (GlobalData.Settings.NavigationType)
            {
                case NavigationType.Automatic:
                    {
                        if (!UrlChecker.IsHttpUrl(url))
                            url = SearchEngineUtil.GetUrl(url, GlobalData.Settings.SearchEngine);
                    }
                    break;

                case NavigationType.SearchOnly:
                    {
                        url = SearchEngineUtil.GetUrl(url, GlobalData.Settings.SearchEngine);
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
                if (UrlChecker.IsLocalSwfFile(item))
                {
                    WindowManager.ShowSwfPlayer(item);
                }
                else
                {
                    ShowBrowser(item);
                }
            }
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
        }
    }
}
