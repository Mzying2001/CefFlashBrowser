using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Views;

namespace CefFlashBrowser.ViewModels
{
    class MainWindowViewModel : NotificationObject
    {
        public DelegateCommand OpenUrlCommand { get; set; }

        public DelegateCommand UpdateUrlCommand { get; set; }

        public DelegateCommand OpenSettingsWindowCommand { get; set; }

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

        private List<LanguageMenuItemViewModel> _languageMenuItems;

        public List<LanguageMenuItemViewModel> LanguageMenuItems
        {
            get => _languageMenuItems;
            set
            {
                _languageMenuItems = value;
                RaisePropertyChanged("LanguageMenuItems");
            }
        }

        private void LoadLanguageMenu()
        {
            LanguageMenuItems = new List<LanguageMenuItemViewModel>();

            foreach (var item in LanguageManager.SupportedLanguage)
            {
                var viewModel = new LanguageMenuItemViewModel(item);
                viewModel.LanguageSwitched += SetLanguageMenuItemsChecked;
                LanguageMenuItems.Add(viewModel);
            }

            SetLanguageMenuItemsChecked();
        }

        private void SetLanguageMenuItemsChecked()
        {
            foreach (var item in LanguageMenuItems)
                item.IsCurrentLanguage = item.Language == LanguageManager.CurrentLanguage;
        }

        private void OpenUrl()
        {
            string url = Url?.Trim();

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(LanguageManager.GetString("message_emptyUrl"));
                return;
            }

            if(UrlChecker.IsUrl(url))
            {
                BrowserWindow.Popup(url);
            }
            else
            {
                BrowserWindow.Popup(SearchEngine.GetUrl(url, Settings.SearchEngine));
            }
        }

        private void UpdateUrl(object sender)
        {
            Url = (sender as System.Windows.Controls.TextBox)?.Text;
        }

        private void OpenSettingsWindow()
        {
            new SettingsWindow().ShowDialog();
        }

        public MainWindowViewModel()
        {
            LoadLanguageMenu();

            OpenUrlCommand = new DelegateCommand(p => OpenUrl());

            UpdateUrlCommand = new DelegateCommand(UpdateUrl);

            OpenSettingsWindowCommand = new DelegateCommand(p => OpenSettingsWindow());
        }
    }
}
