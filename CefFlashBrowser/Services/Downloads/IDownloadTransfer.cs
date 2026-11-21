using CefFlashBrowser.Models.Downloads;
using System;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.Downloads
{
    public interface IDownloadTransfer
    {
        DownloadRecord Record { get; }
        bool CanPause { get; }
        bool CanResume { get; }
        event EventHandler Changed;
        event EventHandler Finished;
        Task StartAsync();
        Task PauseAsync();
        Task CancelAsync();
        Task ShutdownAsync();
    }
}
