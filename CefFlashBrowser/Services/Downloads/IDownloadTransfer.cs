using CefFlashBrowser.Models.Downloads;
using System;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.Downloads
{
    public interface IDownloadTransfer
    {
        event EventHandler Changed;
        event EventHandler Finished;

        DownloadRecord Record { get; }
        bool CanPause { get; }
        bool CanResume { get; }
        Task StartAsync();
        Task PauseAsync();
        Task CancelAsync();
        Task ShutdownAsync();
    }
}
