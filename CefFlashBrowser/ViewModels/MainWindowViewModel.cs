using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.ViewModels.MenuItemViewModels;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs;
using CefFlashBrowser.Views.Dialogs.JsDialogs;
using SimpleMvvm;
using SimpleMvvm.Command;
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
        public DelegateCommand OpenFavoritesItemCommand { get; set; }
        public DelegateCommand OnDropCommand { get; set; }

        public ObservableCollection<LanguageMenuItemViewModel> LanguageMenuItems { get; set; }

        public string AppVersion
        {
            get => Application.ResourceAssembly.GetName().Version.ToString();
        }

        private string _url;

        public string Url
        {
            get => _url;
            set
            {
                _url = value;
                RaisePropertyChanged("Url");
            }
        }

        private void LoadLanguageMenu()
        {
            LanguageMenuItems = new ObservableCollection<LanguageMenuItemViewModel>();

            foreach (var item in LanguageManager.GetSupportedLanguage())
            {
                var viewModel = new LanguageMenuItemViewModel(item);
                viewModel.LanguageSwitched += UpdateLanguageMenuItemsChecked;
                LanguageMenuItems.Add(viewModel);
            }

            UpdateLanguageMenuItemsChecked();
        }

        private void UpdateLanguageMenuItemsChecked()
        {
            var current = LanguageManager.CurrentLanguage;
            foreach (var item in LanguageMenuItems)
                item.IsSelected = item.Language == current;
        }

        private void OpenUrl()
        {
            string url = Url?.Trim();

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
            Favorites.SaveFavorites();
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

        private void SelectLanguageOnFirstStart()
        {
            if (Settings.NotFirstStart)
                return;

            new SelectLanguageDialog().ShowDialog();
            Settings.NotFirstStart = true;
        }

        private void ViewGithub()
        {
            BrowserWindow.Show("https://github.com/Mzying2001/CefFlashBrowser");
        }

        private void OpenFavoritesItem(object website)
        {
            if (website is Website ws)
                BrowserWindow.Show(ws.Url);
        }

        private void OnDrop(object obj)
        {
            if (obj is DragEventArgs args)
            {
                var data = args.Data.GetData(DataFormats.FileDrop) as string[];
                if (data == null)
                    return;

                foreach (var item in data)
                {
                    if (UrlChecker.IsLocalSwfFile(item))
                        SwfPlayerWindow.Show(item);
                    else
                        BrowserWindow.Show(item);
                }
            }
        }

        public MainWindowViewModel()
        {
            SelectLanguageOnFirstStart();
            LoadLanguageMenu();

            OpenUrlCommand = new DelegateCommand(OpenUrl);
            OpenSettingsWindowCommand = new DelegateCommand(OpenSettingsWindow);
            OpenFavoritesManagerCommand = new DelegateCommand(OpenFavoritesManager);
            LoadSwfCommand = new DelegateCommand(LoadSwf);
            ViewGithubCommand = new DelegateCommand(ViewGithub);
            OpenFavoritesItemCommand = new DelegateCommand(OpenFavoritesItem);
            OnDropCommand = new DelegateCommand(OnDrop);
        }
    }
}
