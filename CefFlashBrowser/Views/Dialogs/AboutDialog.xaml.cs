using CefFlashBrowser.Data;
using CefFlashBrowser.Services;
using CefFlashBrowser.Utils;
using System;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class AboutDialog : Window
    {
        private const string RepositoryUrl = "https://github.com/Mzying2001/CefFlashBrowser";

        public AboutDialog()
        {
            InitializeComponent();
            repositoryUrl.Text = RepositoryUrl;
        }

        private void OnOpenRepository(object sender, RoutedEventArgs e)
        {
            WindowManager.OpenInDefaultBrowser(RepositoryUrl);
        }

        private void OnOpenDataFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                ExplorerHelper.OpenDirectory(GlobalData.DataPath);
            }
            catch (Exception exception)
            {
                LogHelper.LogError("Failed to open the application data directory", exception);
                WindowManager.ShowError(exception.Message);
            }
        }
    }
}
