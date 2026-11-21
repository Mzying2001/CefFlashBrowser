using CefFlashBrowser.Models.Downloads;
using CefFlashBrowser.Services.Downloads;
using CefFlashBrowser.Utils;
using CefSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CefFlashBrowser.Browser.Handlers
{
    public sealed class DownloadHandler : FlashBrowser.Handlers.DownloadHandler
    {
        private readonly object _sync = new object();
        private readonly Dictionary<int, NativeDownloadTransfer> _downloads = new Dictionary<int, NativeDownloadTransfer>();
        private readonly HashSet<int> _finished = new HashSet<int>();
        private readonly Dictionary<int, Tuple<DownloadItem, IDownloadItemCallback>> _pending = new Dictionary<int, Tuple<DownloadItem, IDownloadItemCallback>>();
        private readonly Dictionary<string, DownloadRecord> _responses = new Dictionary<string, DownloadRecord>(StringComparer.Ordinal);
        private bool _retired;

        public bool HasActiveDownloads
        {
            get { lock (_sync) return _downloads.Values.Any(transfer => !transfer.Released.IsCompleted); }
        }

        public Task Retire()
        {
            lock (_sync)
            {
                _retired = true;
                foreach (var pending in _pending.Values)
                    pending.Item2.Dispose();
                _pending.Clear();
                return Task.WhenAll(_downloads.Values.Select(transfer => transfer.Released));
            }
        }

        public void RememberResponse(IRequest request, IResponse response, string page)
        {
            var headers = request.Headers.AllKeys.ToDictionary(key => key, key => request.Headers[key], StringComparer.OrdinalIgnoreCase);
            // URLRequest uses the retained browser context's cookie jar.
            headers.Remove("Cookie");
            headers.Remove("Cookie2");
            var etag = response.Headers["ETag"];
            var record = new DownloadRecord
            {
                Url = request.Url,
                Method = request.Method,
                Headers = headers,
                ETag = etag != null && !etag.StartsWith("W/", StringComparison.OrdinalIgnoreCase) ? etag : null,
                LastModified = response.Headers["Last-Modified"],
                Encoded = !string.IsNullOrEmpty(response.Headers["Content-Encoding"])
                    && response.Headers["Content-Encoding"] != "identity",
                SourcePage = page
            };
            lock (_sync)
            {
                if (_responses.Count >= 128)
                    _responses.Clear();
                if (_responses.TryGetValue(request.Url, out var previous)
                    && (previous.Method != record.Method || previous.ETag != record.ETag
                        || previous.Headers.Count != record.Headers.Count
                        || previous.Headers.Any(header => !record.Headers.TryGetValue(header.Key, out var value) || value != header.Value)))
                    record.Method = string.Empty;
                _responses[request.Url] = record;
            }
        }

        public override void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem item, IBeforeDownloadCallback callback)
        {
            DownloadRecord record;
            lock (_sync)
            {
                if (_retired)
                {
                    callback.Dispose();
                    return;
                }
                if (!_responses.TryGetValue(item.Url, out record))
                    record = new DownloadRecord { Url = item.Url, Method = string.Empty };
                _responses.Remove(item.Url);
                record.Source = "browser";
                record.FileName = UserScriptDownloadPaths.SafeFileName(item.SuggestedFileName);
            }
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var manager = DownloadManagerService.Current;
                    var transfer = new NativeDownloadTransfer(manager, record, callback);
                    lock (_sync)
                    {
                        if (_retired || manager.IsStopping)
                        {
                            callback.Dispose();
                            return;
                        }
                        _downloads[item.Id] = transfer;
                        _ = transfer.Released.ContinueWith(completed =>
                        {
                            lock (_sync)
                            {
                                _downloads.Remove(item.Id);
                                _finished.Add(item.Id);
                            }
                        }, TaskScheduler.Default);
                        if (_pending.TryGetValue(item.Id, out var pending))
                        {
                            transfer.Update(pending.Item1, pending.Item2);
                            _pending.Remove(item.Id);
                        }
                    }
                    manager.Accept(transfer);
                    _ = transfer.StartAsync();
                }
                catch (Exception error)
                {
                    callback.Dispose();
                    LogHelper.LogError("Failed to accept browser download", error);
                }
            }));
        }

        public override void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem item, IDownloadItemCallback callback)
        {
            if (!item.IsValid)
            {
                callback.Dispose();
                return;
            }
            lock (_sync)
            {
                if (_downloads.TryGetValue(item.Id, out var transfer))
                    transfer.Update(item, callback);
                else if (!_retired && !_finished.Contains(item.Id))
                {
                    if (_pending.TryGetValue(item.Id, out var previous))
                        previous.Item2.Dispose();
                    _pending[item.Id] = Tuple.Create(item, callback);
                }
                else
                    callback.Dispose();
            }
        }
    }
}
