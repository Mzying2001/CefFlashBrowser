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
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.Views;
using CefSharp;
using IWshRuntimeLibrary;

namespace CefFlashBrowser.ViewModels
{
    class BrowserWindowViewModel : NotificationObject
    {
        public DelegateCommand LoadUrlCommand { get; set; }

        public DelegateCommand OpenDevToolCommand { get; set; }

        public DelegateCommand ViewSourceCommand { get; set; }

        public DelegateCommand OpenInDefaultBrowserCommand { get; set; }

        public DelegateCommand CreateShortcutCommand { get; set; }

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

        public void SetBrowser(ChromiumFlashBrowser browser)
        {
            Browser = browser;
        }

        public void LoadUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
                Browser.Address = url;
        }

        private void ViewSource()
        {
            BrowserWindow.Popup($"view-source:{Browser.Address}", false);
        }

        private void OpenInDefaultBrowser()
        {
            Process.Start(Browser.Address);
        }

        private void CreateShortcut()
        {
            var sfd = new Microsoft.Win32.SaveFileDialog()
            {
                FileName = Browser.Title,
                Filter = $"{LanguageManager.GetString("filter_shortcut")}|*.lnk",
            };
            if (sfd.ShowDialog() == true)
            {
                var path = GetType().Assembly.Location;
                var arg = Browser.Address;
                var fileName = sfd.FileName;

                WshShortcut shortcut = new WshShell().CreateShortcut(fileName);
                shortcut.TargetPath = path;
                shortcut.Arguments = arg;
                shortcut.Save();
            }
        }

        public BrowserWindowViewModel()
        {
            LoadUrlCommand = new DelegateCommand(p => LoadUrl(p?.ToString()));

            OpenDevToolCommand = new DelegateCommand(p => Browser.ShowDevTools());

            ViewSourceCommand = new DelegateCommand(p => ViewSource());

            OpenInDefaultBrowserCommand = new DelegateCommand(p => OpenInDefaultBrowser());

            CreateShortcutCommand = new DelegateCommand(p => CreateShortcut());
        }
    }
}
