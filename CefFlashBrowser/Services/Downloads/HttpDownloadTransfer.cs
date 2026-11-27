using CefFlashBrowser.Models.Downloads;
using Mzying2001.MonkeySharp.Core.Apis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.Downloads
{
    public sealed class HttpDownloadTransfer : IDownloadTransfer, IUserScriptHttpObserver
    {
        private readonly IHttpRequestService _http;
        private readonly DownloadRequest _request;
        private readonly Func<DownloadRecord, CancellationToken, Task<bool>> _selectDestination;
        private CancellationTokenSource _cancellation;
        private Task _run = Task.CompletedTask;
        private FileStream _output;
        private bool _pause;
        private bool _cancel;
        private bool _shutdown;
        private int _terminal;
        private long _offset;
        private long _speedBytes;
        private Exception _responseError;
        private CancellationToken _requestToken;
        private readonly Stopwatch _speedClock = new Stopwatch();
        private TimeSpan _remaining;

        public DownloadRecord Record { get; }
        public bool CanPause => Record.State == DownloadState.Downloading || Record.State == DownloadState.Waiting;
        public bool CanResume => Record.State == DownloadState.Paused || Record.State == DownloadState.Interrupted;
        public event EventHandler Changed;
        public event EventHandler Finished;

        public HttpDownloadTransfer(DownloadRecord record, IHttpRequestService http,
            Func<DownloadRecord, CancellationToken, Task<bool>> selectDestination, DownloadRequest request = null)
        {
            Record = record;
            _http = http;
            _request = request;
            _selectDestination = selectDestination;
            _remaining = request?.Timeout ?? TimeSpan.FromMinutes(10);
        }

        public Task StartAsync()
        {
            lock (Record.SyncRoot)
            {
                if (!_run.IsCompleted || Record.State == DownloadState.Completed || Record.State == DownloadState.Cancelled)
                    return _run;

                _pause = false;
                _cancel = false;
                _shutdown = false;
                _cancellation = new CancellationTokenSource();
                Record.State = DownloadState.Waiting;
                Record.Error = null;
                _run = Task.Run(() => RunAsync(_cancellation.Token));
                return _run;
            }
        }

        public Task PauseAsync() => StopAsync(false, false);
        public Task CancelAsync() => StopAsync(true, false);
        public Task ShutdownAsync() => StopAsync(false, true);

        private async Task StopAsync(bool cancel, bool shutdown)
        {
            Task running;

            lock (Record.SyncRoot)
            {
                if (Record.State == DownloadState.Completed)
                    return;

                _cancel |= cancel;
                _pause = !_cancel;
                _shutdown |= shutdown;
                _cancellation?.Cancel();
                running = _run;
            }

            await running.ConfigureAwait(false);

            if (shutdown && Record.State == DownloadState.Paused)
            {
                lock (Record.SyncRoot)
                    Record.State = DownloadState.Interrupted;

                Changed?.Invoke(this, EventArgs.Empty);
            }

            if (cancel)
            {
                lock (Record.SyncRoot)
                {
                    Record.State = DownloadState.Cancelled;
                    Record.Speed = 0;
                    CleanupCancelledFiles();
                }
                Changed?.Invoke(this, EventArgs.Empty);
                NotifyFinished();
            }
        }

        private async Task RunAsync(CancellationToken token)
        {
            var active = new Stopwatch();
            using (var timeout = new CancellationTokenSource())
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token))
            {
                try
                {
                    _requestToken = linked.Token;

                    if (string.IsNullOrEmpty(Record.FilePath)
                        && !await _selectDestination(Record, linked.Token).ConfigureAwait(false))
                        throw new OperationCanceledException();

                    token.ThrowIfCancellationRequested();

                    Directory.CreateDirectory(Path.GetDirectoryName(Record.FilePath));
                    Record.PartialPath = Record.PartialPath ?? Record.FilePath + "." + Record.Id + ".part";

                    timeout.CancelAfter(_remaining > TimeSpan.Zero ? _remaining : TimeSpan.FromMilliseconds(1));
                    active.Start();

                    if (Record.IsLocalBody)
                        await StageBodyAsync(linked.Token).ConfigureAwait(false);

                    lock (Record.SyncRoot)
                    {
                        Record.State = DownloadState.Downloading;
                    }

                    Changed?.Invoke(this, EventArgs.Empty);

                    using (_output = new FileStream(Record.PartialPath, FileMode.OpenOrCreate,
                        FileAccess.ReadWrite, FileShare.Read, 64 * 1024))
                    {
                        _offset = _output.Length;

                        if (Record.Encoded || (string.IsNullOrEmpty(Record.ETag) && string.IsNullOrEmpty(Record.LastModified)
                            && !Record.IsLocalBody))
                        {
                            if (_offset > 0)
                                Record.Error = "downloads_restartRequired";
                            _offset = 0;
                        }

                        _output.SetLength(_offset);
                        _output.Position = _offset;
                        Record.Received = _offset;
                        _speedBytes = _offset;
                        _speedClock.Restart();

                        if (Record.IsLocalBody)
                            await CopyBodyAsync(linked.Token).ConfigureAwait(false);
                        else
                            await DownloadAsync(linked.Token).ConfigureAwait(false);

                        linked.Token.ThrowIfCancellationRequested();

                        if (Record.Total.HasValue && Record.Received != Record.Total.Value)
                            throw new IOException("The downloaded file length does not match the response length.");

                        _output.Flush(true);
                    }

                    _output = null;

                    lock (Record.SyncRoot)
                    {
                        linked.Token.ThrowIfCancellationRequested();
                        Record.FilePath = DownloadFiles.Commit(Record.PartialPath, Record.FilePath, Record.Overwrite);
                        Record.FileName = Path.GetFileName(Record.FilePath);
                        Record.PartialPath = null;
                        Record.State = DownloadState.Completed;
                        Record.Finished = DateTime.UtcNow;
                        Record.FileMissing = false;
                    }

                    try
                    {
                        DownloadFiles.DeletePartial(Record.BodyPath);
                        Record.BodyPath = null;
                    }
                    catch (IOException error)
                    {
                        Record.Error = error.Message;
                    }
                    catch (UnauthorizedAccessException error)
                    {
                        Record.Error = error.Message;
                    }
                }
                catch (OperationCanceledException)
                {
                    lock (Record.SyncRoot)
                    {
                        Record.State = _cancel ? DownloadState.Cancelled : _shutdown ? DownloadState.Interrupted
                            : _pause ? DownloadState.Paused : timeout.IsCancellationRequested ? DownloadState.TimedOut
                            : DownloadState.Cancelled;
                    }
                }
                catch (Exception error)
                {
                    lock (Record.SyncRoot)
                    {
                        Record.State = _cancel ? DownloadState.Cancelled : _shutdown ? DownloadState.Interrupted
                            : _pause ? DownloadState.Paused : timeout.IsCancellationRequested ? DownloadState.TimedOut : DownloadState.Failed;
                        Record.Error = error.Message;
                    }
                }
                finally
                {
                    _output = null;
                    active.Stop();
                    _remaining -= active.Elapsed;

                    lock (Record.SyncRoot)
                    {
                        Record.Speed = 0;
                        _cancellation.Dispose();
                        _cancellation = null;
                    }

                    if (Record.State == DownloadState.Cancelled)
                        CleanupCancelledFiles();

                    Changed?.Invoke(this, EventArgs.Empty);

                    if (Record.State != DownloadState.Paused && Record.State != DownloadState.Interrupted)
                        NotifyFinished();
                }
            }
        }

        private async Task StageBodyAsync(CancellationToken token)
        {
            if (!string.IsNullOrEmpty(Record.BodyPath) && File.Exists(Record.BodyPath))
                return;

            if (_request?.Body == null)
                throw new IOException("The original page data is no longer available. Start this download again from its source.");

            var path = Record.PartialPath + ".source";

            try
            {
                using (var input = _request.Body.OpenRead())
                using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await input.CopyToAsync(output, 64 * 1024, token).ConfigureAwait(false);
                    output.Flush(true);
                }
                Record.BodyPath = path;
            }
            catch
            {
                DownloadFiles.DeletePartial(path);
                throw;
            }
        }

        private async Task CopyBodyAsync(CancellationToken token)
        {
            using (var input = File.OpenRead(Record.BodyPath))
            {
                Record.Total = input.Length;

                if (_offset > input.Length)
                    throw new IOException("The saved download data is invalid.");

                input.Position = _offset;
                var buffer = new byte[64 * 1024];
                int count;

                while ((count = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    OnResponseData(buffer, 0, count);
                }
            }
        }

        private async Task DownloadAsync(CancellationToken token)
        {
            try
            {
                await SendAsync(token).ConfigureAwait(false);
            }
            catch (RestartDownloadException) when (_offset > 0)
            {
                token.ThrowIfCancellationRequested();
                _output.SetLength(0);
                _output.Position = 0;
                _offset = 0;
                Record.Received = 0;
                _speedBytes = 0;
                _speedClock.Restart();
                Record.Error = "downloads_restartRequired";
                await SendAsync(token).ConfigureAwait(false);
            }
        }

        private async Task SendAsync(CancellationToken token)
        {
            _responseError = null;

            var headers = Record.Headers.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
            headers.Remove("Range");
            headers.Remove("If-Range");
            headers["Accept-Encoding"] = "identity";

            if (_offset > 0)
            {
                headers["Range"] = "bytes=" + _offset.ToString(CultureInfo.InvariantCulture) + "-";
                headers["If-Range"] = !string.IsNullOrEmpty(Record.ETag) ? Record.ETag : Record.LastModified;
            }

            var request = new UserScriptHttpRequest("GET", new Uri(Record.Url), headers, null, null, null,
                _request?.RedirectAllowed ?? ((target, cancellation) => Task.FromResult(true)),
                new UserScriptHttpRequestOptions { Anonymous = Record.Anonymous }, _request?.OriginFrame);
            var operation = _http.SendAsync(request, this, token);

            UserScriptHttpResponse response;

            try
            {
                response = await operation.Completion.ConfigureAwait(false);
            }
            catch (Exception) when (_responseError != null)
            {
                throw _responseError;
            }

            if (_responseError != null)
                throw _responseError;

            Record.Url = response.FinalUrl.AbsoluteUri;
        }

        public void OnResponseStarted(UserScriptHttpResponse response)
        {
            try
            {
                ValidateResponse(response);
            }
            catch (Exception error)
            {
                _responseError = error;
            }
        }

        private void ValidateResponse(UserScriptHttpResponse response)
        {
            if (response.Status == 204 || response.Status == 205)
                throw new IOException("Download HTTP status " + response.Status + " has no content.");

            response.Headers.TryGetValue("ETag", out var etag);
            response.Headers.TryGetValue("Last-Modified", out var modified);
            response.Headers.TryGetValue("Content-Encoding", out var encoding);

            var encoded = !string.IsNullOrEmpty(encoding) && !string.Equals(encoding, "identity", StringComparison.OrdinalIgnoreCase);

            if (_offset > 0 && (response.Status != 206 || encoded
                || !string.IsNullOrEmpty(Record.ETag) && etag != Record.ETag
                || string.IsNullOrEmpty(Record.ETag) && modified != Record.LastModified))
                throw new RestartDownloadException();

            if (response.Status < 200 || response.Status >= 300)
                throw new IOException("Download HTTP status " + response.Status + ".");

            long? total = null;

            if (response.Status == 206)
            {
                response.Headers.TryGetValue("Content-Range", out var range);
                var match = Regex.Match(range ?? string.Empty, @"^bytes (\d+)-(\d+)/(\d+)$", RegexOptions.CultureInvariant);

                if (!match.Success || !long.TryParse(match.Groups[1].Value, out var first) || first != _offset
                    || !long.TryParse(match.Groups[2].Value, out var last) || last < first
                    || !long.TryParse(match.Groups[3].Value, out var length) || last >= length)
                    throw new RestartDownloadException();

                total = length;
            }
            else if (!encoded && response.Headers.TryGetValue("Content-Length", out var value)
                && long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var length))
            {
                total = length;
            }

            lock (Record.SyncRoot)
            {
                Record.Total = total;
                Record.ETag = etag != null && !etag.StartsWith("W/", StringComparison.OrdinalIgnoreCase) ? etag : null;
                Record.LastModified = modified;
                Record.Encoded = encoded;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        public void OnResponseData(byte[] buffer, int offset, int count)
        {
            lock (Record.SyncRoot)
            {
                _requestToken.ThrowIfCancellationRequested();

                if (_responseError != null)
                    throw _responseError;

                _output.Write(buffer, offset, count);
                Record.Received += count;

                if (_speedClock.ElapsedMilliseconds >= 500)
                {
                    Record.Speed = (Record.Received - _speedBytes) / _speedClock.Elapsed.TotalSeconds;
                    _speedBytes = Record.Received;
                    _speedClock.Restart();
                    Changed?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public void OnUploadProgress(long loaded, long? total) { }
        public void OnDownloadProgress(long loaded, long? total) { }

        private void NotifyFinished()
        {
            if (Interlocked.Exchange(ref _terminal, 1) == 0)
                Finished?.Invoke(this, EventArgs.Empty);
        }

        private void CleanupCancelledFiles()
        {
            try
            {
                DownloadFiles.DeletePartial(Record.PartialPath);
                DownloadFiles.DeletePartial(Record.BodyPath);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Record.State = DownloadState.Failed;
                Record.Error = error.Message;
            }
        }

        private sealed class RestartDownloadException : IOException
        {
            public RestartDownloadException() : base("The server response cannot safely resume this download.") { }
        }
    }
}
