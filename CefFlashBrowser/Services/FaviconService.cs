using CefFlashBrowser.Log;
using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    public class FaviconService : INotifyPropertyChanged
    {
        private readonly DatabaseService _database;
        private readonly ILogger _logger;
        private readonly SynchronizationContext _context;
        private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<string, IconMapping> _icons = new Dictionary<string, IconMapping>(StringComparer.Ordinal);
        private readonly List<IconMapping> _pending = new List<IconMapping>();
        private Task _initializeTask;
        private Task _work = Task.CompletedTask;
        private bool _initialized;
        private bool _stopping;

        public event PropertyChangedEventHandler PropertyChanged;
        public int Revision { get; private set; }

        public FaviconService(DatabaseService database, ILogger logger = null)
        {
            _logger = logger;

            _database = database
                ?? throw new ArgumentNullException(nameof(database));

            _context = SynchronizationContext.Current
                ?? throw new InvalidOperationException("Create the favicon service on the UI synchronization context.");
        }

        private void VerifyAccess()
        {
            if (Thread.CurrentThread.ManagedThreadId != _threadId)
                throw new InvalidOperationException("Favicon mappings must be accessed on the UI thread.");
        }

        private static Uri GetPageUri(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsHttp(uri))
                return null;

            return uri;
        }

        private static string GetPageKey(Uri uri)
        {
            return new UriBuilder(uri) { Query = string.Empty, Fragment = string.Empty }.Uri.AbsoluteUri.TrimEnd('/');
        }

        private static bool IsHttp(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static string GetAbsoluteIconUrl(Uri pageUri, string iconUrl)
        {
            return !string.IsNullOrWhiteSpace(iconUrl)
                && Uri.TryCreate(pageUri, iconUrl, out var uri) && IsHttp(uri)
                ? uri.AbsoluteUri : null;
        }

        // Ignores page queries, fragments and trailing slashes. Matches pages before
        // same-origin homepages; schemeless addresses try HTTPS then HTTP at each step.
        public string GetIconUrl(string pageUrl)
        {
            VerifyAccess();

            if (!_initialized || string.IsNullOrWhiteSpace(pageUrl))
                return null;

            pageUrl = pageUrl.Trim();
            var uri = GetPageUri(pageUrl);

            if (uri != null)
                return (GetPageIcon(uri) ?? GetRootIcon(uri))?.IconUrl;

            // A host with a port can itself parse as a non-HTTP URI scheme.
            // Validate the HTTP candidate instead of rejecting that form up front.
            int authorityEnd = pageUrl.IndexOfAny(new[] { '/', '?', '#' });
            int schemeEnd = pageUrl.IndexOf("://", StringComparison.Ordinal);
            int suffixStart = pageUrl.IndexOfAny(new[] { '?', '#' });
            var address = suffixStart < 0 ? pageUrl : pageUrl.Substring(0, suffixStart);

            if ((schemeEnd >= 0 && schemeEnd < authorityEnd)
                || pageUrl.StartsWith("/", StringComparison.Ordinal) || address.Contains("\\"))
                return null;

            var https = GetPageUri("https://" + pageUrl);
            var http = GetPageUri("http://" + pageUrl);

            if (https == null || http == null || !string.IsNullOrEmpty(https.UserInfo)
                || !(https.HostNameType == UriHostNameType.IPv4 || https.HostNameType == UriHostNameType.IPv6
                    || https.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || https.Host.Contains(".")))
                return null;

            return (GetPageIcon(https) ?? GetPageIcon(http) ?? GetRootIcon(https) ?? GetRootIcon(http))?.IconUrl;
        }

        private IconMapping GetPageIcon(Uri uri)
        {
            return _icons.TryGetValue(GetPageKey(uri), out var mapping) ? mapping : null;
        }

        private IconMapping GetRootIcon(Uri uri)
        {
            return _icons.TryGetValue(uri.GetLeftPart(UriPartial.Authority), out var mapping) ? mapping : null;
        }

        private bool ApplyMapping(IconMapping mapping)
        {
            if (_icons.TryGetValue(mapping.PageUrl, out var current) && current.IconUrl == mapping.IconUrl)
                return false;

            _icons[mapping.PageUrl] = mapping;
            return true;
        }

        public Task InitializeAsync()
        {
            VerifyAccess();

            if (_initializeTask != null)
                return _initializeTask;

            if (_stopping)
                return Task.CompletedTask;

            var loadTask = LoadAsync();
            _work = loadTask;

            foreach (var item in _pending)
                _work = PersistAsync(_work, item);

            return _initializeTask = PublishAsync(loadTask);
        }

        private async Task<List<IconMapping>> LoadAsync()
        {
            try
            {
                var records = await _database.GetWebsiteIconsAsync().ConfigureAwait(false);
                var mappings = new List<IconMapping>();
                foreach (var record in records)
                {
                    var pageUri = GetPageUri(record.PageUrl);
                    var iconUrl = pageUri == null ? null : GetAbsoluteIconUrl(pageUri, record.IconUrl);
                    if (iconUrl != null)
                        mappings.Add(new IconMapping(pageUri, iconUrl, record.UpdatedAt));
                }
                return mappings;
            }
            catch (Exception e)
            {
                LogError("Failed to load favicon mappings", e);
                return new List<IconMapping>();
            }
        }

        private async Task PublishAsync(Task<List<IconMapping>> loadTask)
        {
            var snapshot = await loadTask.ConfigureAwait(false);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _context.Post(_ =>
            {
                try
                {
                    if (!_stopping)
                    {
                        foreach (var item in snapshot)
                        {
                            if (item != null)
                                ApplyMapping(item);
                        }

                        foreach (var item in _pending)
                            ApplyMapping(item);

                        _pending.Clear();
                        _initialized = true;
                        NotifyChanged();
                    }
                    completion.SetResult(true);
                }
                catch (Exception e) { completion.SetException(e); }
            }, null);
            await completion.Task.ConfigureAwait(false);
        }

        public Task RecordAsync(string pageUrl, string iconUrl)
        {
            VerifyAccess();

            if (_stopping)
                return Task.CompletedTask;

            var pageUri = GetPageUri(pageUrl);
            var absoluteIconUrl = pageUri == null ? null : GetAbsoluteIconUrl(pageUri, iconUrl);

            if (absoluteIconUrl == null)
                return Task.CompletedTask;

            string key = GetPageKey(pageUri);
            var current = _initialized ? GetPageIcon(pageUri) : _pending.FindLast(item => item.PageUrl == key);
            var mapping = current != null && current.IconUrl == absoluteIconUrl ? current
                : new IconMapping(pageUri, absoluteIconUrl, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            bool changed = _initialized && ApplyMapping(mapping);

            if (!_initialized)
                _pending.Add(mapping);

            // Repeated discoveries still reach persistence so a failed write can be retried.
            var work = _initializeTask == null ? Task.CompletedTask
                : _work = PersistAsync(_work, mapping);

            // A change notification may synchronously report another discovery.
            // Queue this write first so reentrant discoveries retain their order.
            if (changed)
                NotifyChanged();

            return work;
        }

        private async Task PersistAsync(Task previous, IconMapping mapping)
        {
            await previous.ConfigureAwait(false);
            try
            {
                await _database.SaveWebsiteIconAsync(
                    new WebsiteIconRecord(mapping.PageUrl, mapping.IconUrl, mapping.UpdatedAt)).ConfigureAwait(false);
            }
            catch (Exception e) { LogError("Failed to save a favicon mapping", e); }
        }

        private void NotifyChanged()
        {
            Revision++;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Revision)));
        }

        private void LogError(string message, Exception e)
        {
            try { _logger?.Log(LogLevel.Error, message, e); }
            catch { }
        }

        public Task StopAsync()
        {
            VerifyAccess();
            _stopping = true;
            // Persistence never waits for snapshot publication on the UI thread.
            return _work;
        }

        private sealed class IconMapping
        {
            public string PageUrl { get; }
            public string IconUrl { get; }
            public long UpdatedAt { get; }

            public IconMapping(Uri pageUri, string iconUrl, long updatedAt)
            {
                PageUrl = GetPageKey(pageUri);
                IconUrl = iconUrl;
                UpdatedAt = updatedAt;
            }
        }
    }
}
