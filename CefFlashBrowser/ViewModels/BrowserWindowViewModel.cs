using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.FlashBrowser;
using CefSharp;

namespace CefFlashBrowser.ViewModels
{
    class BrowserWindowViewModel : NotificationObject
    {
        public DelegateCommand LoadUrlCommand { get; set; }

        public DelegateCommand OpenDevToolCommand { get; set; }

        public DelegateCommand ViewSourceCommand { get; set; }

        public DelegateCommand OpenInDefaultBrowserCommand { get; set; }

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

        private void OpenInDefaultBrowser()
        {
            Process.Start(Browser.Address);
        }

        public BrowserWindowViewModel()
        {
            LoadUrlCommand = new DelegateCommand(p => LoadUrl(p?.ToString()));

            OpenDevToolCommand = new DelegateCommand(p => Browser.ShowDevTools());

            ViewSourceCommand = new DelegateCommand(p => Browser.ViewSource());

            OpenInDefaultBrowserCommand = new DelegateCommand(p => OpenInDefaultBrowser());
        }
    }
}
