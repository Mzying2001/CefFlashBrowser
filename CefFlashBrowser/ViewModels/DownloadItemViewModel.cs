using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Services.Downloads;
using SimpleMvvm;
using System;

namespace CefFlashBrowser.ViewModels
{
    public sealed class DownloadItemViewModel : ViewModelBase
    {
        private readonly Func<bool> _canRecover;

        public DownloadRecord Record { get; }

        public DownloadItemViewModel(DownloadRecord record, Func<bool> canRecover)
        {
            Record = record;
            _canRecover = canRecover;
        }

        public string FileName => Record.FileName;
        public string FilePath => Record.FilePath;
        public string Url => Record.Url;
        public string Source => DownloadManagerService.Text("downloads_" + Record.Source);
        public string State => DownloadManagerService.Text(Record.FileMissing ? "downloads_missing" : "downloads_" + Record.State);
        public string Error => Record.State == DownloadState.Interrupted && !_canRecover()
            ? DownloadManagerService.Text("downloads_sourceUnavailable")
            : (Record.Error?.StartsWith("downloads_", StringComparison.Ordinal) == true ? DownloadManagerService.Text(Record.Error) : Record.Error);
        public string Size => FormatSize(Record.Received) + " / " + (Record.Total.HasValue ? FormatSize(Record.Total.Value) : "—");
        public string Speed => Record.State == DownloadState.Downloading ? FormatSize(Record.Speed) + "/s" : "—";
        public double Progress => Record.State == DownloadState.Completed ? 100 : Record.Total > 0 ? Math.Min(100, 100.0 * Record.Received / Record.Total.Value) : 0;
        public bool IsIndeterminate => Record.State == DownloadState.Downloading && !Record.Total.HasValue;
        public string ProgressText => Record.State == DownloadState.Completed || Record.Total > 0 ? Progress.ToString("0.#") + "%" : "—";
        public bool IsActive => Record.State == DownloadState.Waiting || Record.State == DownloadState.Downloading || Record.State == DownloadState.Paused;
        public bool CanOpen => Record.State == DownloadState.Completed && !Record.FileMissing;

        public void Refresh()
        {
            RaisePropertyChanged(string.Empty);
        }

        private static string FormatSize(double size)
        {
            var units = new[] { "B", "KB", "MB", "GB", "TB" };
            int unit = 0;

            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return size.ToString("0.##") + " " + units[unit];
        }
    }
}
