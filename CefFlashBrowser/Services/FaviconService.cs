using CefFlashBrowser.Log;
using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
        private readonly Dictionary<string, WebsiteIconRecord> _icons = new Dictionary<string, WebsiteIconRecord>(StringComparer.Ordinal);
        private readonly List<IconDiscovery> _pending = new List<IconDiscovery>();
        private Task _initializeTask;
        private Task<IconSnapshot> _initialWriteState;
        private IReadOnlyDictionary<string, List<string>> _legacyKeys;
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
            return uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.UriEscaped).TrimEnd('/');
        }

        private static string GetRootKey(Uri uri)
        {
            return uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped);
        }

        private static bool IsHttp(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static string GetAbsoluteIconUrl(Uri pageUri, string iconUrl)
        {
            if (ImageDataUrl.HasScheme(iconUrl))
                return ImageDataUrl.TryParse(iconUrl, out _, out _, out _, out _) ? iconUrl : null;

            return !string.IsNullOrWhiteSpace(iconUrl)
                && Uri.TryCreate(pageUri, iconUrl, out var uri) && IsHttp(uri) ? uri.AbsoluteUri : null;
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

        private WebsiteIconRecord GetPageIcon(Uri uri)
        {
            return GetMapping(_icons, GetPageKey(uri));
        }

        private WebsiteIconRecord GetRootIcon(Uri uri)
        {
            return GetMapping(_icons, GetRootKey(uri));
        }

        private static WebsiteIconRecord GetMapping(Dictionary<string, WebsiteIconRecord> icons, string key)
        {
            return icons.TryGetValue(key, out var mapping) ? mapping : null;
        }

        private static WebsiteIconRecord SetMapping(Dictionary<string, WebsiteIconRecord> icons, string key,
            IconDiscovery discovery, out bool changed)
        {
            var current = GetMapping(icons, key);
            changed = current?.IconUrl != discovery.IconUrl;

            if (!changed)
                return current;

            return icons[key] = new WebsiteIconRecord(key, discovery.IconUrl, discovery.UpdatedAt);
        }

        private static IconWrite ApplyDiscovery(Dictionary<string, WebsiteIconRecord> icons, IconDiscovery discovery, out bool changed)
        {
            changed = false;

            if (discovery.PageKey == discovery.RootKey)
                return new IconWrite(SetMapping(icons, discovery.RootKey, discovery, out changed));

            var root = GetMapping(icons, discovery.RootKey) ?? SetMapping(icons, discovery.RootKey, discovery, out changed);
            if (root.IconUrl == discovery.IconUrl)
            {
                changed |= icons.Remove(discovery.PageKey);
                return new IconWrite(root, pageToRemove: discovery.PageKey);
            }

            var page = SetMapping(icons, discovery.PageKey, discovery, out changed);
            return new IconWrite(root, page);
        }

        public Task InitializeAsync()
        {
            VerifyAccess();

            if (_initializeTask != null)
                return _initializeTask;

            if (_stopping)
                return Task.CompletedTask;

            var loadTask = LoadAsync();
            _work = _initialWriteState = CopySnapshotAsync(loadTask);

            foreach (var item in _pending)
                _work = PersistPendingAsync(_work, _initialWriteState, item);

            return _initializeTask = PublishAsync(loadTask);
        }

        private async Task<IconSnapshot> LoadAsync()
        {
            try
            {
                var snapshot = new IconSnapshot();
                var records = await _database.GetWebsiteIconsAsync().ConfigureAwait(false);

                foreach (var record in records)
                {
                    var pageUri = GetPageUri(record.PageUrl);
                    var iconUrl = pageUri == null ? null : GetAbsoluteIconUrl(pageUri, record.IconUrl);
                    if (iconUrl == null)
                        continue;

                    string key = GetPageKey(pageUri);
                    if (GetMapping(snapshot.Mappings, key)?.IconUrl != iconUrl)
                        snapshot.Mappings[key] = new WebsiteIconRecord(key, iconUrl, record.UpdatedAt);

                    if (record.PageUrl != key)
                    {
                        if (!snapshot.LegacyKeys.TryGetValue(key, out var keys))
                            snapshot.LegacyKeys[key] = keys = new List<string>();

                        keys.Add(record.PageUrl);
                    }
                }
                return snapshot;
            }
            catch (Exception e)
            {
                LogError("Failed to load favicon mappings", e);
                return new IconSnapshot();
            }
        }

        private static async Task<IconSnapshot> CopySnapshotAsync(Task<IconSnapshot> loadTask)
        {
            var snapshot = await loadTask.ConfigureAwait(false);
            return new IconSnapshot(snapshot);
        }

        private async Task PublishAsync(Task<IconSnapshot> loadTask)
        {
            var snapshot = await loadTask.ConfigureAwait(false);
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            _context.Post(state =>
            {
                try
                {
                    if (!_stopping)
                    {
                        foreach (var item in snapshot.Mappings)
                            _icons.Add(item.Key, item.Value);

                        _legacyKeys = snapshot.LegacyKeys;
                        foreach (var item in _pending)
                            ApplyDiscovery(_icons, item, out _);

                        _pending.Clear();
                        _initialWriteState = null;
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

            var discovery = new IconDiscovery(pageUri, absoluteIconUrl);
            if (!_initialized)
            {
                _pending.Add(discovery);
                return _initialWriteState == null ? Task.CompletedTask
                    : _work = PersistPendingAsync(_work, _initialWriteState, discovery);
            }

            var write = ApplyDiscovery(_icons, discovery, out bool changed);

            // Repeated discoveries still reach persistence so a failed write can be retried.
            // A change notification may synchronously report another discovery.
            // Queue this write first so reentrant discoveries retain their order.
            var work = _work = PersistAsync(_work, write);

            if (changed)
                NotifyChanged();
            return work;
        }

        private async Task PersistPendingAsync(Task previous, Task<IconSnapshot> initialState, IconDiscovery discovery)
        {
            // Resolve pending writes against the loaded snapshot without waiting for the UI thread to publish it.
            await previous.ConfigureAwait(false);
            var snapshot = await initialState.ConfigureAwait(false);
            var write = ApplyDiscovery(snapshot.Mappings, discovery, out _);
            await SaveAsync(write, snapshot.LegacyKeys).ConfigureAwait(false);
        }

        private async Task PersistAsync(Task previous, IconWrite write)
        {
            await previous.ConfigureAwait(false);
            await SaveAsync(write, _legacyKeys).ConfigureAwait(false);
        }

        private async Task SaveAsync(IconWrite write, IReadOnlyDictionary<string, List<string>> legacyKeys)
        {
            try
            {
                var keysToRemove = GetObsoleteKeys(write, legacyKeys).ToArray();
                await _database.SaveWebsiteIconDiscoveryAsync(write.Root, write.Page, keysToRemove).ConfigureAwait(false);
            }
            catch (Exception e) { LogError("Failed to save a favicon mapping", e); }
        }

        private static IEnumerable<string> GetObsoleteKeys(IconWrite write, IReadOnlyDictionary<string, List<string>> legacyKeys)
        {
            if (legacyKeys.TryGetValue(write.Root.PageUrl, out var rootKeys))
            {
                foreach (string key in rootKeys)
                    yield return key;
            }

            string pageKey = write.Page?.PageUrl ?? write.PageToRemove;

            if (pageKey != null && legacyKeys.TryGetValue(pageKey, out var pageKeys))
            {
                foreach (string key in pageKeys)
                    yield return key;
            }

            if (write.PageToRemove != null)
            {
                yield return write.PageToRemove;
            }
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

        private sealed class IconSnapshot
        {
            public Dictionary<string, WebsiteIconRecord> Mappings { get; }
            public Dictionary<string, List<string>> LegacyKeys { get; }

            public IconSnapshot()
            {
                Mappings = new Dictionary<string, WebsiteIconRecord>(StringComparer.Ordinal);
                LegacyKeys = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            }

            public IconSnapshot(IconSnapshot snapshot)
            {
                Mappings = new Dictionary<string, WebsiteIconRecord>(snapshot.Mappings, StringComparer.Ordinal);
                LegacyKeys = snapshot.LegacyKeys;
            }
        }

        private sealed class IconDiscovery
        {
            public string RootKey { get; }
            public string PageKey { get; }
            public string IconUrl { get; }
            public long UpdatedAt { get; }

            public IconDiscovery(Uri pageUri, string iconUrl)
            {
                RootKey = GetRootKey(pageUri);
                PageKey = GetPageKey(pageUri);
                IconUrl = iconUrl;
                UpdatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            }
        }

        private sealed class IconWrite
        {
            public WebsiteIconRecord Root { get; }
            public WebsiteIconRecord Page { get; }
            public string PageToRemove { get; }

            public IconWrite(WebsiteIconRecord root, WebsiteIconRecord page = null, string pageToRemove = null)
            {
                Root = root;
                Page = page;
                PageToRemove = pageToRemove;
            }
        }
    }
}
