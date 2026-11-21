using CefFlashBrowser.Data;
using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using CefFlashBrowser.Views.Dialogs;
using CefSharp;
using Mzying2001.MonkeySharp.CefSharp;
using Mzying2001.MonkeySharp.Core.Apis;
using SimpleMvvm.Ioc;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CefFlashBrowser.Services.Downloads
{
    public sealed class DownloadManagerService
    {
        private static DownloadManagerService _current;
        public static DownloadManagerService Current => _current ?? (_current = new DownloadManagerService());
        public static Task StopForExitAsync() => _current?.StopAsync() ?? Task.CompletedTask;

        private readonly Dispatcher _dispatcher;
        private readonly DownloadHistory _history;
        private readonly Dictionary<string, IDownloadTransfer> _transfers = new Dictionary<string, IDownloadTransfer>();
        private readonly DispatcherTimer _timer;
        private readonly SemaphoreSlim _dialogs = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _saves = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private CefSharpHttpRequestService _http;
        private IRequestContext _context;
        private Task _stop;
        private bool _dirty;
        private int _ticks;

        public ObservableCollection<DownloadItemViewModel> Items { get; } = new ObservableCollection<DownloadItemViewModel>();
        public bool IsStopping => _stop != null;
        public event EventHandler Refreshed;

        public DownloadManagerService()
        {
            _dispatcher = Application.Current.Dispatcher;
            _dispatcher.VerifyAccess();
            _history = new DownloadHistory(Path.Combine(GlobalData.DataPath, "downloads.json"));
            try
            {
                foreach (var record in _history.Load().OrderByDescending(record => record.Created))
                    Items.Add(new DownloadItemViewModel(record));
            }
            catch (Exception error) { LogHelper.LogError("Failed to load download history", error); }
            _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, Tick, _dispatcher);
        }

        public IHttpRequestService Http
        {
            get
            {
                _dispatcher.VerifyAccess();
                if (_http == null)
                {
                    _context = Cef.GetGlobalRequestContext();
                    _http = new CefSharpHttpRequestService(_context);
                }
                return _http;
            }
        }

        public async Task NewAsync()
        {
            if (IsStopping)
                return;
            var record = new DownloadRecord { Source = "manual" };
            if (!await SelectDestinationAsync(record, _lifetime.Token))
                return;
            var transfer = new HttpDownloadTransfer(record, Http, SelectDestinationAsync);
            Accept(transfer);
            _ = transfer.StartAsync();
        }

        public void Accept(IDownloadTransfer transfer)
        {
            _dispatcher.VerifyAccess();
            if (IsStopping)
                throw new InvalidOperationException("The download manager is stopping.");
            _transfers[transfer.Record.Id] = transfer;
            transfer.Changed += TransferChanged;
            transfer.Finished += TransferFinished;
            if (!Items.Any(item => item.Record.Id == transfer.Record.Id))
                Items.Insert(0, new DownloadItemViewModel(transfer.Record));
            _dirty = true;
            Refresh();
        }

        public IDownloadTransfer Find(DownloadRecord record)
        {
            _dispatcher.VerifyAccess();
            return _transfers.TryGetValue(record.Id, out var transfer) ? transfer : null;
        }

        public bool CanRestore(DownloadRecord record)
        {
            return record.Method == "GET" && (record.IsLocalBody ? File.Exists(record.BodyPath)
                : Uri.TryCreate(record.Url, UriKind.Absolute, out var url) && (url.Scheme == "http" || url.Scheme == "https"));
        }

        public async Task ResumeAsync(DownloadRecord record)
        {
            if (IsStopping)
                return;
            var transfer = Find(record);
            if (transfer == null || !transfer.CanResume)
            {
                if (!CanRestore(record))
                    throw new IOException(Text("downloads_sourceUnavailable"));
                if (transfer != null)
                    Unsubscribe(transfer);
                record.State = DownloadState.Interrupted;
                record.Finished = null;
                transfer = new HttpDownloadTransfer(record, Http, SelectDestinationAsync);
                Accept(transfer);
            }
            _ = transfer.StartAsync();
            await SaveAsync();
        }

        public async Task PauseAsync(DownloadRecord record)
        {
            var transfer = Find(record);
            if (transfer != null)
                await transfer.PauseAsync();
            Refresh();
            await SaveAsync();
        }

        public async Task CancelAsync(DownloadRecord record)
        {
            var transfer = Find(record);
            if (transfer != null)
                await transfer.CancelAsync();
            else
            {
                DownloadFiles.DeletePartial(record.PartialPath);
                DownloadFiles.DeletePartial(record.BodyPath);
                record.State = DownloadState.Cancelled;
            }
            Refresh();
            await SaveAsync();
        }

        public async Task RemoveAsync(DownloadItemViewModel item)
        {
            if (item.IsActive && !Confirm("downloads_confirmRemoveActive"))
                return;
            if (item.Record.State != DownloadState.Completed)
                await CancelAsync(item.Record);
            if (_transfers.TryGetValue(item.Record.Id, out var transfer))
                Unsubscribe(transfer);
            _transfers.Remove(item.Record.Id);
            Items.Remove(item);
            await SaveAsync();
        }

        public async Task DeleteFileAsync(DownloadRecord record)
        {
            if (!Confirm("downloads_confirmDeleteFile"))
                return;
            File.Delete(record.FilePath);
            Refresh();
            await SaveAsync();
        }

        public async Task ClearFinishedAsync()
        {
            foreach (var item in Items.Where(item => !item.IsActive && item.Record.State != DownloadState.Interrupted).ToArray())
            {
                if (item.Record.State != DownloadState.Completed)
                {
                    DownloadFiles.DeletePartial(item.Record.PartialPath);
                    DownloadFiles.DeletePartial(item.Record.BodyPath);
                }
                if (_transfers.TryGetValue(item.Record.Id, out var transfer))
                    Unsubscribe(transfer);
                _transfers.Remove(item.Record.Id);
                Items.Remove(item);
            }
            await SaveAsync();
        }

        public async Task<bool> SelectDestinationAsync(DownloadRecord record, CancellationToken token)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime.Token))
            {
                await _dialogs.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    return await _dispatcher.InvokeAsync(() =>
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        var dialog = WindowManager.CreateWindow<DownloadDialog>();
                        dialog.Initialize(record);
                        using (linked.Token.Register(() => _dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (dialog.IsVisible)
                                dialog.Close();
                        }))))
                            return DialogHelper.ShowModal(dialog) == true;
                    });
                }
                finally { _dialogs.Release(); }
            }
        }

        private void TransferChanged(object sender, EventArgs e)
        {
            Post(() => { _dirty = true; Refresh(); });
        }

        private void TransferFinished(object sender, EventArgs e)
        {
            Post(() => { _dirty = true; Refresh(); _ = SaveAsync(); });
        }

        private void Post(Action action)
        {
            if (!_dispatcher.HasShutdownStarted)
                _dispatcher.BeginInvoke(action);
        }

        private void Tick(object sender, EventArgs e)
        {
            Refresh();
            if (++_ticks % 4 == 0 && _dirty)
                _ = SaveAsync();
        }

        public void Refresh()
        {
            foreach (var item in Items)
            {
                item.Record.FileMissing = item.Record.State == DownloadState.Completed && !File.Exists(item.Record.FilePath);
                item.Refresh();
            }
            Refreshed?.Invoke(this, EventArgs.Empty);
        }

        public async Task SaveAsync()
        {
            var records = Items.Select(item => item.Record).ToArray();
            _dirty = false;
            await _saves.WaitAsync();
            try { await Task.Run(() => _history.Save(records)); }
            catch (Exception error) { LogHelper.LogError("Failed to save download history", error); }
            finally { _saves.Release(); }
        }

        private void Unsubscribe(IDownloadTransfer transfer)
        {
            transfer.Changed -= TransferChanged;
            transfer.Finished -= TransferFinished;
        }

        public Task StopAsync() => _stop ?? (_stop = StopCoreAsync());

        private async Task StopCoreAsync()
        {
            await Task.Yield();
            _timer.Stop();
            _lifetime.Cancel();
            await Task.WhenAll(_transfers.Values.Select(transfer => transfer.ShutdownAsync()));
            await SaveAsync();
            _http?.Dispose();
            _context?.Dispose();
        }

        public static string Text(string key) => SimpleIoc.Global.GetInstance<LanguageService>().GetString(key);
        private static bool Confirm(string key)
        {
            bool result = false;
            WindowManager.Confirm(Text(key), Text("downloads_title"), value => result = value == true);
            return result;
        }
    }
}
