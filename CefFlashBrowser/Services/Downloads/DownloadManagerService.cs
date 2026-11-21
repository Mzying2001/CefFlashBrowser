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
using System.Windows.Interop;
using System.Windows.Threading;

namespace CefFlashBrowser.Services.Downloads
{
    public sealed class DownloadManagerService
    {
        private static DownloadManagerService _current;
        public static DownloadManagerService Current => _current ?? (_current = new DownloadManagerService());
        public static Task StopForExitAsync() => _current?.StopAsync() ?? Task.CompletedTask;

        private readonly Dispatcher _dispatcher;
        private readonly DatabaseService _database;
        private readonly Dictionary<string, IDownloadTransfer> _transfers = new Dictionary<string, IDownloadTransfer>();
        private readonly List<Task> _cleanup = new List<Task>();
        private readonly DispatcherTimer _timer;
        private readonly SemaphoreSlim _dialogs = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _saves = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private CefSharpHttpRequestService _http;
        private IRequestContext _context;
        private Task _stop;
        // A null record represents a pending deletion for that ID.
        private Dictionary<string, DownloadRecord> _pending = new Dictionary<string, DownloadRecord>();
        private int _ticks;
        private bool _checkingFiles;
        private HwndSource _nativeHost;
        private readonly HashSet<WinformCefSharp4WPF.ChromiumWebBrowser> _retainedBrowsers = new HashSet<WinformCefSharp4WPF.ChromiumWebBrowser>();

        public ObservableCollection<DownloadItemViewModel> Items { get; } = new ObservableCollection<DownloadItemViewModel>();
        public bool IsStopping => _stop != null;
        public event EventHandler Refreshed;

        public DownloadManagerService()
        {
            _dispatcher = Application.Current.Dispatcher;
            _dispatcher.VerifyAccess();
            _database = SimpleIoc.Global.GetInstance<DatabaseService>();

            try
            {
                foreach (var record in _database.GetDownloadsAsync().GetAwaiter().GetResult())
                    Items.Add(CreateItem(record));
            }
            catch (Exception error)
            {
                LogHelper.LogError("Failed to load download history", error);
            }

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
                Items.Insert(0, CreateItem(transfer.Record));

            _pending[transfer.Record.Id] = transfer.Record;
            Refresh();
        }

        public IDownloadTransfer Find(DownloadRecord record)
        {
            _dispatcher.VerifyAccess();
            return _transfers.TryGetValue(record.Id, out var transfer) ? transfer : null;
        }

        private DownloadItemViewModel CreateItem(DownloadRecord record)
        {
            return new DownloadItemViewModel(record, () => Find(record)?.CanResume == true || CanRestore(record));
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
            await SaveRecordAsync(record);
        }

        public async Task PauseAsync(DownloadRecord record)
        {
            var transfer = Find(record);
            if (transfer == null)
                return;

            await transfer.PauseAsync();
            Refresh();
            await SaveRecordAsync(record);
        }

        public async Task CancelAsync(DownloadRecord record)
        {
            await CancelTransferAsync(record);
            Refresh();
            await SaveRecordAsync(record);
        }

        private async Task CancelTransferAsync(DownloadRecord record)
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
        }

        public async Task RemoveAsync(DownloadItemViewModel item)
        {
            if (item.IsActive && !Confirm("downloads_confirmRemoveActive"))
                return;

            if (item.Record.State != DownloadState.Completed)
                await CancelTransferAsync(item.Record);

            RemoveItem(item);
            await SaveAsync();
        }

        public Task DeleteFileAsync(DownloadRecord record)
        {
            if (!Confirm("downloads_confirmDeleteFile"))
                return Task.CompletedTask;

            File.Delete(record.FilePath);
            Refresh();
            return Task.CompletedTask;
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
                RemoveItem(item);
            }

            await SaveAsync();
        }

        private void RemoveItem(DownloadItemViewModel item)
        {
            var id = item.Record.Id;

            if (_transfers.TryGetValue(id, out var transfer))
                Unsubscribe(transfer);

            _transfers.Remove(id);
            _pending[id] = null;
            Items.Remove(item);
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
                        {
                            return DialogHelper.ShowModal(dialog) == true;
                        }
                    });
                }
                finally
                {
                    _dialogs.Release();
                }
            }
        }

        private void TransferChanged(object sender, EventArgs e)
        {
            var transfer = (IDownloadTransfer)sender;

            Post(() =>
            {
                if (!IsCurrentTransfer(transfer))
                    return;

                _pending[transfer.Record.Id] = transfer.Record;
                Refresh(false);
            });
        }

        private void TransferFinished(object sender, EventArgs e)
        {
            var transfer = (IDownloadTransfer)sender;

            Post(() =>
            {
                if (!IsCurrentTransfer(transfer))
                    return;

                Unsubscribe(transfer);
                _transfers.Remove(transfer.Record.Id);
                _pending[transfer.Record.Id] = transfer.Record;
                Refresh(false);

                if (!IsStopping)
                    _ = SaveAsync();
            });
        }

        private bool IsCurrentTransfer(IDownloadTransfer transfer) =>
            _transfers.TryGetValue(transfer.Record.Id, out var current) && ReferenceEquals(current, transfer);

        private void Post(Action action)
        {
            if (!_dispatcher.HasShutdownStarted)
                _dispatcher.BeginInvoke(action);
        }

        private void Tick(object sender, EventArgs e)
        {
            Refresh(false);

            if (++_ticks % 4 == 0)
            {
                if (Application.Current.Windows.OfType<Views.DownloadManagerWindow>().Any(window => window.IsVisible))
                    _ = CheckFilesAsync();

                if (_pending.Count != 0 && _saves.CurrentCount != 0)
                    _ = SaveAsync();
            }
        }

        public void Refresh(bool checkFiles = true)
        {
            foreach (var item in Items)
                item.Refresh();

            Refreshed?.Invoke(this, EventArgs.Empty);

            if (checkFiles)
                _ = CheckFilesAsync();
        }

        private async Task CheckFilesAsync()
        {
            if (_checkingFiles || IsStopping)
                return;

            _checkingFiles = true;

            try
            {
                var records = Items.Where(item => item.Record.State == DownloadState.Completed)
                    .Select(item => Tuple.Create(item.Record, item.Record.FilePath)).ToArray();
                var missing = await Task.Run(() => records.Select(item => !File.Exists(item.Item2)).ToArray());

                for (int index = 0; index < records.Length; index++)
                {
                    var record = records[index].Item1;

                    if (record.State == DownloadState.Completed && record.FilePath == records[index].Item2)
                        record.FileMissing = missing[index];
                }

                Refresh(false);
            }
            finally
            {
                _checkingFiles = false;
            }
        }

        internal Task SaveRecordAsync(DownloadRecord record)
        {
            _dispatcher.VerifyAccess();

            if (!Items.Any(item => ReferenceEquals(item.Record, record)))
                return Task.CompletedTask;

            _pending[record.Id] = record;
            return SaveAsync();
        }

        private async Task SaveAsync()
        {
            await _saves.WaitAsync();
            var batch = _pending;

            try
            {
                if (batch.Count == 0)
                    return;

                _pending = new Dictionary<string, DownloadRecord>();
                var upserts = batch.Values.Where(record => record != null).ToArray();
                var removedIds = batch.Where(change => change.Value == null).Select(change => change.Key).ToArray();
                await _database.ApplyDownloadChangesAsync(upserts, removedIds);
            }
            catch (Exception error)
            {
                foreach (var change in batch)
                {
                    if (!_pending.ContainsKey(change.Key))
                        _pending.Add(change.Key, change.Value);
                }
                LogHelper.LogError("Failed to save download history", error);
            }
            finally
            {
                _saves.Release();
            }
        }

        private void Unsubscribe(IDownloadTransfer transfer)
        {
            transfer.Changed -= TransferChanged;
            transfer.Finished -= TransferFinished;
        }

        public Task StopAsync() => _stop ?? (_stop = StopCoreAsync());

        public void RetainUntil(Task completion, Action release)
        {
            RetainUntil(completion, () =>
            {
                release();
                return Task.CompletedTask;
            });
        }

        public void RetainUntil(Task completion, Func<Task> release)
        {
            _dispatcher.VerifyAccess();
            _cleanup.RemoveAll(task => task.Status == TaskStatus.RanToCompletion);
            _cleanup.Add(ReleaseAsync(completion, release));
        }

        public bool RetainBrowser(WinformCefSharp4WPF.ChromiumWebBrowser browser, Browser.Handlers.DownloadHandler handler)
        {
            _dispatcher.VerifyAccess();

            if (_retainedBrowsers.Contains(browser))
                return true;

            if (!handler.HasActiveDownloads || IsStopping)
                return false;

            if (_nativeHost == null)
            {
                _nativeHost = new HwndSource(new HwndSourceParameters("CefFlashBrowser downloads")
                {
                    WindowStyle = unchecked((int)0x80000000),
                    Width = 1,
                    Height = 1
                });
            }

            var lease = browser.RetainNativeHost(_nativeHost.Handle);
            _retainedBrowsers.Add(browser);

            RetainUntil(handler.Retire(), () =>
            {
                lease.Dispose();
                _retainedBrowsers.Remove(browser);
            });
            return true;
        }

        private static async Task ReleaseAsync(Task completion, Func<Task> release)
        {
            try
            {
                await completion;
            }
            finally
            {
                await release();
            }
        }

        private async Task StopCoreAsync()
        {
            await Task.Yield();

            _timer.Stop();
            _lifetime.Cancel();

            await Task.WhenAll(_transfers.Values.Select(transfer => transfer.ShutdownAsync()));
            await Task.WhenAll(_cleanup.ToArray());

            foreach (var transfer in _transfers.Values)
                _pending[transfer.Record.Id] = transfer.Record;

            await SaveAsync();

            _http?.Dispose();

            if (_http != null)
                await Cef.UIThreadTaskFactory.StartNew(() => { });

            _context?.Dispose();
            _nativeHost?.Dispose();
        }

        public static string Text(string key)
        {
            return SimpleIoc.Global.GetInstance<LanguageService>().GetString(key);
        }

        private static bool Confirm(string key)
        {
            bool result = false;
            WindowManager.Confirm(Text(key), Text("downloads_title"), value => result = value == true);
            return result;
        }
    }
}
