using CefFlashBrowser.Models;
using CefFlashBrowser.Models.Data;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Messaging;
using System.Collections.ObjectModel;
using System.Windows;

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
        public DelegateCommand OnDropCommand { get; set; }
        public DelegateCommand SwitchLanguageCommand { get; set; }

        public ObservableCollection<string> Language { get; set; }

        private void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                JsAlertDialog.Show(LanguageManager.GetString("message_emptyUrl"));
                return;
            }

            if (UrlChecker.IsLocalSwfFile(url))
            {
                SwfPlayerWindow.Show(url);
                return;
            }

            /*
             * Address Bar Function
             * 
             * 0: Automatic
             * 1: Search Only
             * 2: Navigate Only
             */
            switch (Settings.AddressBarFunction)
            {
                case 0:
                    if (!UrlChecker.IsUrl(url))
                        url = SearchEngine.GetUrl(url, Settings.SearchEngine);
                    break;

                case 1:
                    url = SearchEngine.GetUrl(url, Settings.SearchEngine);
                    break;

                case 2:
                    //nothing to do
                    break;
            }

            BrowserWindow.Show(url);
        }

        private void OpenSettingsWindow()
        {
            new SettingsWindow().ShowDialog();
        }

        private void OpenFavoritesManager()
        {
            new FavoritesManager().ShowDialog();
        }

        private void LoadSwf()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = $"{LanguageManager.GetString("filter_swf")}|*.swf"
            };
            if (ofd.ShowDialog() == true)
            {
                SwfPlayerWindow.Show(ofd.FileName);
            }
        }

        private void ViewGithub()
        {
            BrowserWindow.Show("https://github.com/Mzying2001/CefFlashBrowser");
        }

        private void OpenWebsite(Website website)
        {
            BrowserWindow.Show(website.Url);
        }

        private void OnDrop(DragEventArgs args)
        {
            var data = args.Data.GetData(DataFormats.FileDrop) as string[];
            if (data != null)
            {
                foreach (var item in data)
                {
                    if (UrlChecker.IsLocalSwfFile(item))
                        SwfPlayerWindow.Show(item);
                    else
                        BrowserWindow.Show(item);
                }
            }
        }

        private void SelectLanguageOnFirstStart()
        {
            if (Settings.NotFirstStart)
                return;

            new SelectLanguageDialog().ShowDialog();
            Settings.NotFirstStart = true;
        }

        private void SwitchLanguage(string language)
        {
            LanguageManager.CurrentLanguage = language;
            Messenger.Global.Send(MessageTokens.LANGUAGE_CHANGED, language);
        }

        public MainWindowViewModel()
        {
            SelectLanguageOnFirstStart();

            Language = new ObservableCollection<string>();
            foreach (var item in LanguageManager.GetSupportedLanguage())
                Language.Add(item);

            OpenUrlCommand = new DelegateCommand<string>(OpenUrl);
            OpenSettingsWindowCommand = new DelegateCommand(OpenSettingsWindow);
            OpenFavoritesManagerCommand = new DelegateCommand(OpenFavoritesManager);
            LoadSwfCommand = new DelegateCommand(LoadSwf);
            ViewGithubCommand = new DelegateCommand(ViewGithub);
            OpenWebsiteCommand = new DelegateCommand<Website>(OpenWebsite);
            OnDropCommand = new DelegateCommand<DragEventArgs>(OnDrop);
            SwitchLanguageCommand = new DelegateCommand<string>(SwitchLanguage);
        }
    }
}
