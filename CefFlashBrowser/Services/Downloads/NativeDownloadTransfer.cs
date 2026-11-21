using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Utils;
using CefSharp;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.Downloads
{
    public sealed class NativeDownloadTransfer : IDownloadTransfer
    {
        private readonly DownloadManagerService _manager;
        private readonly IBeforeDownloadCallback _before;
        private readonly CancellationTokenSource _selection = new CancellationTokenSource();
        private readonly SemaphoreSlim _commands = new SemaphoreSlim(1, 1);
        private readonly TaskCompletionSource<bool> _released = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private IDownloadItemCallback _callback;
        private bool _started;
        private bool _stopping;
        private bool _paused;
        private int _terminal;

        public DownloadRecord Record { get; }
        public Task Released => _released.Task;
        public bool CanPause => Record.State == DownloadState.Downloading && _callback != null;
        public bool CanResume => (Record.State == DownloadState.Paused || Record.State == DownloadState.Interrupted)
            && _callback != null && !_released.Task.IsCompleted;
        public event EventHandler Changed;
        public event EventHandler Finished;

        public NativeDownloadTransfer(DownloadManagerService manager, DownloadRecord record, IBeforeDownloadCallback before)
        {
            _manager = manager;
            Record = record;
            _before = before;
        }

        public async Task StartAsync()
        {
            if (_started)
            {
                await ResumeNativeAsync();
                return;
            }
            _started = true;
            try
            {
                if (!await _manager.SelectDestinationAsync(Record, _selection.Token))
                {
                    await CancelAsync();
                    return;
                }
                await _commands.WaitAsync();
                try
                {
                    if (_stopping)
                        return;
                    Record.PartialPath = Record.FilePath + "." + Record.Id + ".part";
                    await Cef.UIThreadTaskFactory.StartNew(() =>
                    {
                        if (!_before.IsDisposed)
                            _before.Continue(Record.PartialPath, false);
                        _before.Dispose();
                    });
                    Record.State = DownloadState.Downloading;
                    Changed?.Invoke(this, EventArgs.Empty);
                    await _manager.SaveAsync();
                }
                finally { _commands.Release(); }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                await ControlAsync(callback => callback.Cancel());
                _before.Dispose();
                Record.Error = error.Message;
                Record.State = DownloadState.Failed;
                Release();
            }
        }

        public void Update(DownloadItem item, IDownloadItemCallback callback)
        {
            lock (Record.SyncRoot)
            {
                if (_stopping || Volatile.Read(ref _terminal) != 0)
                {
                    callback.Dispose();
                    return;
                }
                var previous = _callback;
                _callback = callback;
                previous?.Dispose();
                Record.Received = item.ReceivedBytes;
                Record.Total = item.TotalBytes > 0 ? (long?)item.TotalBytes : null;
                Record.Speed = _paused ? 0 : Math.Max(0, item.CurrentSpeed);
                if (item.IsComplete)
                {
                    _stopping = true;
                    _ = CommitAsync();
                    return;
                }
                if (item.IsCancelled)
                {
                    Record.State = DownloadState.Interrupted;
                    Record.Speed = 0;
                    Record.Error = "downloads_nativeInterrupted";
                }
                else if (!string.IsNullOrEmpty(Record.PartialPath))
                    Record.State = _paused ? DownloadState.Paused : DownloadState.Downloading;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private async Task CommitAsync()
        {
            await _commands.WaitAsync();
            try
            {
                var path = await Task.Run(() => DownloadFiles.Commit(Record.PartialPath, Record.FilePath, Record.Overwrite));
                lock (Record.SyncRoot)
                {
                    Record.FilePath = path;
                    Record.FileName = Path.GetFileName(path);
                    Record.PartialPath = null;
                    Record.Received = new FileInfo(path).Length;
                    Record.Total = Record.Received;
                    Record.State = DownloadState.Completed;
                    Record.Finished = DateTime.UtcNow;
                    Record.Speed = 0;
                }
            }
            catch (Exception error)
            {
                Record.State = DownloadState.Failed;
                Record.Error = error.Message;
            }
            finally { _commands.Release(); Release(); }
        }

        public async Task PauseAsync()
        {
            await _commands.WaitAsync();
            try
            {
                if (!CanPause)
                    return;
                _paused = true;
                await ControlAsync(callback => callback.Pause());
                Record.State = DownloadState.Paused;
                Record.Speed = 0;
                Changed?.Invoke(this, EventArgs.Empty);
            }
            finally { _commands.Release(); }
        }

        public async Task ResumeNativeAsync()
        {
            await _commands.WaitAsync();
            try
            {
                if (!CanResume)
                    return;
                _paused = false;
                Record.State = DownloadState.Downloading;
                Record.Error = null;
                await ControlAsync(callback => callback.Resume());
                Changed?.Invoke(this, EventArgs.Empty);
            }
            finally { _commands.Release(); }
        }

        private Task ControlAsync(Action<IDownloadItemCallback> control)
        {
            return Cef.UIThreadTaskFactory.StartNew(() =>
            {
                lock (Record.SyncRoot)
                {
                    if (_callback != null && !_callback.IsDisposed)
                        control(_callback);
                }
            });
        }

        public Task CancelAsync() => StopAsync(false);
        public Task ShutdownAsync() => StopAsync(true);

        private async Task StopAsync(bool preserve)
        {
            await _commands.WaitAsync();
            if (_stopping || _released.Task.IsCompleted)
            {
                _commands.Release();
                await _released.Task;
                return;
            }
            try
            {
                _stopping = true;
                _selection.Cancel();
                var originalPartial = Record.PartialPath;
                if (preserve)
                {
                    await ControlAsync(callback => callback.Pause());
                    await SnapshotAsync();
                }
                await ControlAsync(callback => callback.Cancel());
                _before.Dispose();
                await WaitForNativeCleanupAsync(originalPartial);
                Record.State = preserve ? DownloadState.Interrupted : DownloadState.Cancelled;
                Record.Speed = 0;
            }
            catch (Exception error)
            {
                Record.State = DownloadState.Failed;
                Record.Error = error.Message;
            }
            finally { _commands.Release(); Release(); }
        }

        private static async Task WaitForNativeCleanupAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            var clock = Stopwatch.StartNew();
            while (File.Exists(path) && clock.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(25);
            DownloadFiles.DeletePartial(path);
        }

        private async Task SnapshotAsync()
        {
            var partial = Record.PartialPath;
            if (string.IsNullOrEmpty(partial) || !File.Exists(partial))
                return;
            var saved = partial + ".resume";
            await Task.Run(async () =>
            {
                using (var input = new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var output = new FileStream(saved, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    long remaining = Math.Min(input.Length, Record.Received);
                    var buffer = new byte[64 * 1024];
                    while (remaining > 0)
                    {
                        var count = await input.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining));
                        if (count == 0)
                            break;
                        await output.WriteAsync(buffer, 0, count);
                        remaining -= count;
                    }
                    output.Flush(true);
                    Record.Received = output.Length;
                }
            });
            Record.PartialPath = saved;
        }

        private void Release()
        {
            if (Interlocked.Exchange(ref _terminal, 1) != 0)
                return;
            lock (Record.SyncRoot)
            {
                _callback?.Dispose();
                _callback = null;
            }
            try
            {
                Changed?.Invoke(this, EventArgs.Empty);
                Finished?.Invoke(this, EventArgs.Empty);
            }
            finally { _released.TrySetResult(true); }
        }
    }
}
