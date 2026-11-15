using CefFlashBrowser.Services;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using Mzying2001.MonkeySharp.Core.Apis;
using SimpleMvvm.Ioc;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace CefFlashBrowser.Browser.UserScripts
{
    public sealed class UserScriptDownloadService : IDownloadService, IDisposable
    {
        private static readonly ConditionalWeakTable<Window, SemaphoreSlim> Pickers = new ConditionalWeakTable<Window, SemaphoreSlim>();
        private readonly object _lock = new object();
        private readonly Dictionary<string, Operation> _operations = new Dictionary<string, Operation>();
        private readonly FileDownloadService _pipeline;
        private readonly BrowserViewModel _model;
        private readonly Func<Window> _owner;
        private readonly Dispatcher _dispatcher;
        private readonly string _directory;
        private readonly CancellationToken _lifetime;
        private bool _accepting = true, _disposed;
        public string DownloadMode => _pipeline.DownloadMode;
        public int ActiveCount { get { lock (_lock) return _operations.Count; } }

        public UserScriptDownloadService(IHttpRequestService http, BrowserViewModel model, Func<Window> owner,
            Dispatcher dispatcher, string directory, CancellationToken lifetime)
        {
            _model = model;
            _owner = owner;
            _dispatcher = dispatcher;
            _directory = Path.GetFullPath(directory);
            _lifetime = lifetime;
            _pipeline = new FileDownloadService(http, SelectDestinationAsync);
        }

        public Task<IDownloadOperation> CreateAsync(DownloadRequest request, CancellationToken token)
        {
            lock (_lock)
            {
                if (!_accepting) throw new ObjectDisposedException(nameof(UserScriptDownloadService));
                _lifetime.ThrowIfCancellationRequested();
                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _lifetime);
                try
                {
                    // The pinned FileDownloadService only allocates an unstarted
                    // operation here; no UI, network or file work is awaited.
                    var inner = _pipeline.CreateAsync(request, cancellation.Token).GetAwaiter().GetResult();
                    var name = SuggestedName(request);
                    var operation = new Operation(this, inner, name, cancellation);
                    _operations.Add(operation.DownloadId, operation);
                    _ = operation.ObserveAsync();
                    return Task.FromResult<IDownloadOperation>(operation);
                }
                catch { cancellation.Dispose(); throw; }
            }
        }

        private static string SuggestedName(DownloadRequest request) => UserScriptDownloadPaths.SafeFileName(
            string.IsNullOrWhiteSpace(request.Name) ? Path.GetFileName(request.Url.AbsolutePath) : request.Name);

        private async Task<DownloadDestination> SelectDestinationAsync(DownloadRequest request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_directory);
            var name = SuggestedName(request);
            var path = Path.Combine(_directory, name);
            var prompt = request.SaveAs || request.ConflictAction == "prompt"
                || request.ConflictAction == "overwrite" && File.Exists(path);
            if (!prompt) return new DownloadDestination(path, false);
            var window = await _dispatcher.InvokeAsync(() =>
            {
                token.ThrowIfCancellationRequested();
                return _owner() ?? throw new InvalidOperationException("The download owner is not available.");
            });
            var picker = Pickers.GetValue(window, key => new SemaphoreSlim(1, 1));
            await picker.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var selected = await _dispatcher.InvokeAsync(() =>
                {
                    token.ThrowIfCancellationRequested();
                    var title = SimpleIoc.Global.GetInstance<LanguageService>().GetString("userscript_saveDownload");
                    return UserScriptDownloadFilePicker.Select(window, title, name, _directory, token);
                });
                return selected == null ? null : new DownloadDestination(selected, true);
            }
            finally { picker.Release(); }
        }

        private void Post(Action action)
        {
            if (_lifetime.IsCancellationRequested || _dispatcher.HasShutdownStarted) return;
            _dispatcher.BeginInvoke(new Action(() => { if (!_lifetime.IsCancellationRequested && !_model.IsClosed) action(); }));
        }

        public void Retire() { lock (_lock) _accepting = false; }
        public Task DrainAsync() { lock (_lock) return Task.WhenAll(_operations.Values.Select(operation => operation.Drained).ToArray()); }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;
                _disposed = true;
                _accepting = false;
            }
            _pipeline.Dispose();
        }

        private sealed class Operation : IDownloadOperation
        {
            private readonly UserScriptDownloadService _owner;
            private readonly IDownloadOperation _inner;
            private readonly UserScriptDownloadItem _item;
            private readonly CancellationTokenSource _cancellation;
            private readonly TaskCompletionSource<bool> _drained = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            private int _started;
            public Task Drained => _drained.Task;
            public string DownloadId => _inner.DownloadId;
            public DownloadResult Result => _inner.Result;
            public Task Completion => _inner.Completion;
            public event EventHandler<UserScriptDownloadProgress> Progress;
            public event EventHandler Completed, Aborted, TimedOut;
            public event EventHandler<UserScriptDownloadFailure> Failed;

            public Operation(UserScriptDownloadService owner, IDownloadOperation inner, string name, CancellationTokenSource cancellation)
            {
                _owner = owner;
                _inner = inner;
                _cancellation = cancellation;
                _item = new UserScriptDownloadItem(name, inner.Abort);
                inner.Progress += (sender, value) =>
                {
                    owner.Post(() => { _item.Received = value.Loaded; _item.State = UserScriptDownloadState.Downloading; });
                    Progress?.Invoke(this, value);
                };
                inner.Completed += (sender, e) =>
                {
                    owner.Post(() => { _item.FileName = inner.Result.Name; _item.Received = inner.Result.BytesReceived; _item.State = UserScriptDownloadState.Completed; });
                    Completed?.Invoke(this, EventArgs.Empty);
                };
                inner.Aborted += (sender, e) => { owner.Post(() => _item.State = UserScriptDownloadState.Cancelled); Aborted?.Invoke(this, EventArgs.Empty); };
                inner.TimedOut += (sender, e) => { owner.Post(() => _item.State = UserScriptDownloadState.TimedOut); TimedOut?.Invoke(this, EventArgs.Empty); };
                inner.Failed += (sender, e) => { owner.Post(() => _item.State = UserScriptDownloadState.Failed); Failed?.Invoke(this, e); };
            }

            public void Start()
            {
                if (Interlocked.Exchange(ref _started, 1) != 0) return;
                _owner.Post(() => _owner._model.UserScriptDownloads.Add(_item));
                _inner.Start();
            }

            public async Task ObserveAsync()
            {
                try { await _inner.Completion.ConfigureAwait(false); }
                catch (Exception) { /* Terminal events already carry the outcome. */ }
                finally
                {
                    _cancellation.Dispose();
                    lock (_owner._lock) _owner._operations.Remove(DownloadId);
                    _drained.TrySetResult(true);
                }
            }
            public void Abort() => _inner.Abort();
            public void Dispose() => _inner.Dispose();
        }
    }
}
