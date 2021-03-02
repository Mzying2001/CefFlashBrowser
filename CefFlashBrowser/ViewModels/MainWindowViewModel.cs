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

        private void OpenUrl()
        {
            string url = Url?.Trim();

            if (string.IsNullOrEmpty(url))
            {
                MessageBox.Show(Application.Current.Resources["message_emptyUrl"].ToString());
                return;
            }

            if(UrlChecker.IsUrl(url))
            {
                BrowserWindow.Popup(url);
            }
            else
            {
                BrowserWindow.Popup(SearchEngine.GetUrl(url));
            }
        }

        private void OpenSettingsWindow()
        {
            new SettingsWindow().ShowDialog();
        }

        public MainWindowViewModel()
        {
            OpenUrlCommand = new DelegateCommand()
            {
                Execute = p => OpenUrl()
            };

            OpenSettingsWindowCommand = new DelegateCommand()
            {
                Execute = p => OpenSettingsWindow()
            };
        }
    }
}
