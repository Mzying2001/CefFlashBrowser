using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Services;
using CefFlashBrowser.ViewModels.MenuItemViewModels;
using CefFlashBrowser.Views;

namespace CefFlashBrowser.ViewModels
{
    class MainWindowViewModel : NotificationObject
    {
        public DelegateCommand OpenUrlCommand { get; set; }

        public DelegateCommand UpdateUrlCommand { get; set; }

        public DelegateCommand OpenSettingsWindowCommand { get; set; }

        public DelegateCommand OpenFavoritesManagerCommand { get; set; }

        public DelegateCommand LoadSwfCommand { get; set; }

        public ObservableCollection<FavoriteMenuItemVliewModel> FavoriteItems { get; set; }

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

        private void LoadFavoriteItems()
        {
            FavoriteItems = new ObservableCollection<FavoriteMenuItemVliewModel>();
            foreach (var item in new JsonDataService().GetFavorites())
                FavoriteItems.Add(new FavoriteMenuItemVliewModel(item));
        }

        private void OpenUrl()
        {
            string url = Url?.Trim();

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(LanguageManager.GetString("message_emptyUrl"));
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

        private void UpdateUrl(object sender)
        {
            Url = (sender as System.Windows.Controls.TextBox)?.Text;
        }

        private void OpenSettingsWindow()
        {
            new SettingsWindow().ShowDialog();
        }

        private void OpenFavoritesManager()
        {
            new FavoriteManager(FavoriteItems).ShowDialog();
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

        public MainWindowViewModel()
        {
            LoadFavoriteItems();
            LoadLanguageMenu();

            OpenUrlCommand = new DelegateCommand(p => OpenUrl());

            UpdateUrlCommand = new DelegateCommand(UpdateUrl);

            OpenSettingsWindowCommand = new DelegateCommand(p => OpenSettingsWindow());

            OpenFavoritesManagerCommand = new DelegateCommand(p => OpenFavoritesManager());

            LoadSwfCommand = new DelegateCommand(p => LoadSwf());
        }
    }
}
