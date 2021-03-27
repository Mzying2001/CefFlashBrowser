using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.FlashBrowser;

namespace CefFlashBrowser.ViewModels
{
    class BrowserWindowViewModel : NotificationObject
    {
        public DelegateCommand LoadUrlCommand { get; set; }

        public DelegateCommand GoBackCommand { get; set; }

        public DelegateCommand GoForwardCommand { get; set; }

        public ChromiumFlashBrowser Browser { get; set; }

        private double _navigationBarHeight;

        public double NavigationBarHeight
        {
            get => _navigationBarHeight;
            set
            {
                _navigationBarHeight = value;
                RaisePropertyChanged("NavigationBarHeight");
            }
        }

        public bool ShowNavigationBar
        {
            set => NavigationBarHeight = value ? double.NaN : 0;
        }

        private void LoadUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
                Browser.Address = url;
        }

        private void GoBack()
        {
            Browser.BackCommand.Execute(null);
        }

        private void GoForward()
        {
            Browser.ForwardCommand.Execute(null);
        }

        public BrowserWindowViewModel()
        {
            LoadUrlCommand = new DelegateCommand(p => LoadUrl(p?.ToString()));

            GoBackCommand = new DelegateCommand(p => GoBack());

            GoForwardCommand = new DelegateCommand(p => GoForward());
        }
    }
}
