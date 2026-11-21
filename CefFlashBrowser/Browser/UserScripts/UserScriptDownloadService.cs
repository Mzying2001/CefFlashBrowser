using CefFlashBrowser.Data;
using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Services.Downloads;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using Mzying2001.MonkeySharp.Core.Apis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptDownloadService : IDownloadService, IDisposable
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, Operation> _operations = new Dictionary<string, Operation>();
        private readonly IHttpRequestService _http;
        private readonly Dispatcher _dispatcher;
        private readonly string _sourcePage;
        private readonly DownloadManagerService _manager;
        private bool _accepting = true;

        public string DownloadMode => "browser";
        public int ActiveCount { get { lock (_sync) return _operations.Count; } }

        public UserScriptDownloadService(IHttpRequestService http, BrowserViewModel model, Dispatcher dispatcher)
        {
            _http = http;
            _dispatcher = dispatcher;
            _sourcePage = model.Address;
            _manager = DownloadManagerService.Current;
        }

        public Task<IDownloadOperation> CreateAsync(DownloadRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                if (!_accepting || _manager.IsStopping)
                    throw new ObjectDisposedException(nameof(UserScriptDownloadService));
                var operation = new Operation(this, request, token);
                _operations.Add(operation.DownloadId, operation);
                return Task.FromResult<IDownloadOperation>(operation);
            }
        }

        public void Retire() { lock (_sync) _accepting = false; }

        public Task DrainAsync()
        {
            lock (_sync)
                return Task.WhenAll(_operations.Values.Select(operation => operation.Completion).ToArray())
                    .ContinueWith(task => { var observed = task.Exception; }, TaskScheduler.Default);
        }

        public void Dispose()
        {
            Retire();
            Operation[] operations;
            lock (_sync) operations = _operations.Values.ToArray();
            foreach (var operation in operations)
                operation.Dispose();
        }

        private Task<bool> SelectDestinationAsync(DownloadRecord record, DownloadRequest request, CancellationToken token)
        {
            var directory = DownloadFiles.DirectoryOrDefault(GlobalData.Settings.DownloadDirectory);
            var path = Path.Combine(directory, record.FileName);
            if (request.SaveAs || request.ConflictAction == "prompt"
                || request.ConflictAction == "overwrite" && File.Exists(path))
                return _manager.SelectDestinationAsync(record, token);
            record.FilePath = path;
            return Task.FromResult(true);
        }

        private sealed class Operation : IDownloadOperation
        {
            private readonly UserScriptDownloadService _owner;
            private readonly HttpDownloadTransfer _transfer;
            private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly CancellationTokenRegistration _registration;
            private int _started;
            private int _terminal;

            public string DownloadId => _transfer.Record.Id;
            public DownloadResult Result { get; private set; }
            public Task Completion => _completion.Task;
            public event EventHandler<UserScriptDownloadProgress> Progress;
            public event EventHandler Completed;
            public event EventHandler Aborted;
            public event EventHandler TimedOut;
            public event EventHandler<UserScriptDownloadFailure> Failed;

            public Operation(UserScriptDownloadService owner, DownloadRequest request, CancellationToken token)
            {
                _owner = owner;
                var record = new DownloadRecord
                {
                    Url = request.Url.AbsoluteUri,
                    FileName = UserScriptDownloadPaths.SafeFileName(string.IsNullOrWhiteSpace(request.Name)
                        ? Path.GetFileName(request.Url.AbsolutePath) : request.Name),
                    Source = "script",
                    SourcePage = owner._sourcePage,
                    Headers = request.Headers.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase),
                    Anonymous = request.Anonymous,
                    IsLocalBody = request.Body != null
                };
                _transfer = new HttpDownloadTransfer(record, owner._http,
                    (item, cancellation) => owner.SelectDestinationAsync(item, request, cancellation), request);
                _transfer.Changed += TransferChanged;
                _transfer.Finished += TransferFinished;
                _registration = token.Register(Abort);
            }

            public void Start()
            {
                if (Interlocked.Exchange(ref _started, 1) != 0)
                    return;
                _owner._dispatcher.BeginInvoke(new Action(() =>
                {
                    if (Volatile.Read(ref _terminal) != 0)
                        return;
                    try
                    {
                        _owner._manager.Accept(_transfer);
                        _ = _transfer.StartAsync();
                    }
                    catch (Exception error) { Finish(DownloadState.Failed, error); }
                }));
            }

            private void TransferChanged(object sender, EventArgs e)
            {
                var record = _transfer.Record;
                if (record.State == DownloadState.Interrupted && _owner._manager.IsStopping)
                    Finish(DownloadState.Interrupted, null);
                else if (record.State == DownloadState.Downloading)
                    Progress?.Invoke(this, new UserScriptDownloadProgress(record.Received, record.Total));
            }

            private void TransferFinished(object sender, EventArgs e) => Finish(_transfer.Record.State, null);

            private void Finish(DownloadState state, Exception error)
            {
                if (Interlocked.Exchange(ref _terminal, 1) != 0)
                    return;
                _transfer.Changed -= TransferChanged;
                _transfer.Finished -= TransferFinished;
                _registration.Dispose();
                var record = _transfer.Record;
                try
                {
                    switch (state)
                    {
                        case DownloadState.Completed:
                            Result = new DownloadResult(DownloadId, new Uri(record.Url), record.FileName, record.Received);
                            Completed?.Invoke(this, EventArgs.Empty);
                            _completion.TrySetResult(true);
                            break;
                        case DownloadState.Cancelled:
                            Aborted?.Invoke(this, EventArgs.Empty);
                            _completion.TrySetCanceled();
                            break;
                        case DownloadState.Interrupted:
                            _completion.TrySetCanceled();
                            break;
                        case DownloadState.TimedOut:
                            TimedOut?.Invoke(this, EventArgs.Empty);
                            _completion.TrySetException(new TimeoutException("The download timed out."));
                            break;
                        default:
                            error = error ?? new IOException(record.Error ?? "The download failed.");
                            Failed?.Invoke(this, new UserScriptDownloadFailure(error));
                            _completion.TrySetException(error);
                            break;
                    }
                }
                finally
                {
                    lock (_owner._sync) _owner._operations.Remove(DownloadId);
                }
            }

            public void Abort()
            {
                if (Volatile.Read(ref _terminal) != 0)
                    return;
                if (Volatile.Read(ref _started) == 0)
                    Finish(DownloadState.Cancelled, null);
                else
                    _ = _transfer.CancelAsync();
            }

            public void Dispose() => Abort();
        }
    }
}
