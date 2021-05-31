using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Services;
using CefFlashBrowser.ViewModels.MenuItemViewModels;
using CefFlashBrowser.Views;
using CefFlashBrowser.Views.Dialogs;

namespace CefFlashBrowser.ViewModels
{
    class MainWindowViewModel : NotificationObject
    {
        public ICommand OpenUrlCommand { get; set; }
        public ICommand UpdateUrlCommand { get; set; }
        public ICommand OpenSettingsWindowCommand { get; set; }
        public ICommand OpenFavoritesManagerCommand { get; set; }
        public ICommand LoadSwfCommand { get; set; }
        public ICommand ViewGithubCommand { get; set; }
        public ICommand OpenFavoritesItemCommand { get; set; }

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
                MessageBox.Show(LanguageManager.GetString("message_emptyUrl"));
                return;
            }

            if (UrlChecker.IsLocalSwfFile(url))
            {
                BrowserWindow.PopupFlashPlayer(url);
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

            BrowserWindow.Popup(url);
        }

        private void UpdateUrl(string url)
        {
            Url = url;
        }

        private void OpenSettingsWindow()
        {
            new SettingsWindow().ShowDialog();
        }

        private void OpenFavoritesManager()
        {
            new FavoritesManager().ShowDialog();
            new FavoritesDataService().WriteFavorites(Favorites.Items);
        }

        private void LoadSwf()
        {
            var ofd = new Microsoft.Win32.OpenFileDialog()
            {
                Filter = $"{LanguageManager.GetString("filter_swf")}|*.swf"
            };
            if (ofd.ShowDialog() == true)
            {
                BrowserWindow.PopupFlashPlayer(ofd.FileName);
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
            Process.Start("https://github.com/Mzying2001/CefFlashBrowser");
        }

        private void OpenFavoritesItem(object website)
        {
            if (website is Website ws)
                BrowserWindow.Popup(ws.Url);
        }

        public MainWindowViewModel()
        {
            SelectLanguageOnFirstStart();
            LoadLanguageMenu();

            OpenUrlCommand = new DelegateCommand(OpenUrl);
            UpdateUrlCommand = new DelegateCommand(url => UpdateUrl(url?.ToString()));
            OpenSettingsWindowCommand = new DelegateCommand(OpenSettingsWindow);
            OpenFavoritesManagerCommand = new DelegateCommand(OpenFavoritesManager);
            LoadSwfCommand = new DelegateCommand(LoadSwf);
            ViewGithubCommand = new DelegateCommand(ViewGithub);
            OpenFavoritesItemCommand = new DelegateCommand(OpenFavoritesItem);
        }
    }
}
