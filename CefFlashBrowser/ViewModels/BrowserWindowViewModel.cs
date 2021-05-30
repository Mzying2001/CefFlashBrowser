using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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
        public ICommand LoadUrlCommand { get; set; }

        public ICommand OpenDevToolCommand { get; set; }

        public ICommand ViewSourceCommand { get; set; }

        public ICommand OpenInDefaultBrowserCommand { get; set; }

        public ICommand CreateShortcutCommand { get; set; }

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
                FileName = Browser.Title.Replace('.', '_'),
                Filter = $"{LanguageManager.GetString("filter_shortcut")}|*.lnk",
            };

            if (sfd.ShowDialog() == true)
            {
                var path = GetType().Assembly.Location;
                var arg = Browser.Address;
                var fileName = sfd.FileName;

                try
                {
                    WshShortcut shortcut = new WshShell().CreateShortcut(fileName);
                    shortcut.TargetPath = path;
                    shortcut.Arguments = arg;
                    shortcut.Save();
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message);
                }
            }
        }

        public BrowserWindowViewModel()
        {
            LoadUrlCommand = new DelegateCommand(url => LoadUrl(url?.ToString()));

            OpenDevToolCommand = new DelegateCommand(p => Browser.ShowDevTools());

            ViewSourceCommand = new DelegateCommand(ViewSource);

            OpenInDefaultBrowserCommand = new DelegateCommand(OpenInDefaultBrowser);

            CreateShortcutCommand = new DelegateCommand(CreateShortcut);
        }
    }
}
