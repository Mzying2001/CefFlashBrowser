using CefFlashBrowser.Data;
using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.Downloads;
using CefFlashBrowser.Utils;
using Microsoft.Win32;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class DownloadDialog : Window
    {
        private DownloadRecord _record;
        private string _directory;

        internal Func<string, int> StartSystemDownload { get; set; } = Win32.DoFileDownload;
        internal bool SystemDownloadStarted { get; private set; }

        public DownloadDialog()
        {
            InitializeComponent();
        }

        public void Initialize(DownloadRecord record)
        {
            Initialize(record, false);
        }

        internal void Initialize(DownloadRecord record, bool offerSystemDownload)
        {
            _record = record;
            _directory = DownloadFiles.DirectoryOrDefault(GlobalData.Settings.DownloadDirectory);
            urlBox.Text = record.Url ?? string.Empty;
            urlBox.IsReadOnly = record.Source != "manual";
            pathBox.Text = DownloadFiles.AvailablePath(Path.Combine(_directory,
                UserScriptDownloadPaths.SafeFileName(record.FileName)));
            systemDownloadButton.Visibility = offerSystemDownload ? Visibility.Visible : Visibility.Collapsed;
            systemDownloadButton.IsEnabled = offerSystemDownload && record.Method == "GET"
                && Uri.TryCreate(record.Url, UriKind.Absolute, out var url)
                && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps);

            if (string.IsNullOrEmpty(record.Url))
                urlBox.Focus();
        }

        private void UrlChanged(object sender, TextChangedEventArgs e)
        {
            if (_record?.Source == "manual" && Uri.TryCreate(urlBox.Text.Trim(), UriKind.Absolute, out var uri))
                pathBox.Text = DownloadFiles.AvailablePath(Path.Combine(_directory,
                    UserScriptDownloadPaths.SafeFileName(Uri.UnescapeDataString(Path.GetFileName(uri.AbsolutePath)))));
        }

        private void Browse(object sender, RoutedEventArgs e)
        {
            var picker = new SaveFileDialog
            {
                FileName = pathBox.Text,
                AddExtension = false,
                OverwritePrompt = false
            };

            if (picker.ShowDialog(this) == true)
                pathBox.Text = picker.FileName;
        }

        private void StartDownload(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Uri.TryCreate(urlBox.Text.Trim(), UriKind.Absolute, out var url)
                    || _record.Source == "manual" && url.Scheme != "http" && url.Scheme != "https")
                    throw new ArgumentException(DownloadManagerService.Text("downloads_invalidUrl"));

                var path = pathBox.Text.Trim();
                if (!Path.IsPathRooted(path) || Path.GetFileName(path) != UserScriptDownloadPaths.SafeFileName(Path.GetFileName(path)))
                    throw new ArgumentException(DownloadManagerService.Text("downloads_invalidPath"));
                path = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                if (Directory.Exists(path))
                    throw new ArgumentException(DownloadManagerService.Text("downloads_invalidPath"));

                bool overwrite = false;
                if (File.Exists(path))
                {
                    WindowManager.Confirm(DownloadManagerService.Text("downloads_confirmOverwrite"), Title, value => overwrite = value == true);
                    if (!overwrite)
                        return;
                }

                _record.Url = url.AbsoluteUri;
                _record.FilePath = path;
                _record.FileName = Path.GetFileName(path);
                _record.Overwrite = overwrite;

                if (rememberBox.IsChecked == true)
                {
                    GlobalData.Settings.DownloadDirectory = Path.GetDirectoryName(path);
                    GlobalData.SaveSettings();
                }

                DialogHelper.SetDialogResult(this, true);
            }
            catch (Exception error)
            {
                WindowManager.ShowError(error.Message);
            }
        }

        private void SystemDownload(object sender, RoutedEventArgs e)
        {
            if (!systemDownloadButton.IsEnabled || SystemDownloadStarted)
                return;

            IsEnabled = false;
            try
            {
                Marshal.ThrowExceptionForHR(StartSystemDownload(_record.Url), new IntPtr(-1));
                SystemDownloadStarted = true;
            }
            catch (Exception error)
            {
                LogHelper.LogError("Failed to start the system download", error);
            }
            finally
            {
                IsEnabled = true;
            }

            if (!IsVisible)
                return;

            if (SystemDownloadStarted)
            {
                DialogHelper.SetDialogResult(this, false);
            }
            else
            {
                WindowManager.ShowAlertDialog(
                    DownloadManagerService.Text("downloads_systemDownloadFailed"),
                    DownloadManagerService.Text("common_error"), this);
            }
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            DialogHelper.SetDialogResult(this, false);
        }
    }
}
