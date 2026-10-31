using CefFlashBrowser.Database;
using CefFlashBrowser.Log;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    public class FaviconService : IFaviconService
    {
        private readonly IDatabase _database;
        private readonly ILogger _logger;
        private readonly SynchronizationContext _context;
        private readonly int _threadId = Thread.CurrentThread.ManagedThreadId;
        private readonly Dictionary<string, string> _icons = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, string>> _pending = new List<KeyValuePair<string, string>>();
        private Task _initializeTask;
        private Task _work = Task.CompletedTask;
        private bool _initialized;
        private bool _stopping;
        private bool _schemaReady;

        public event PropertyChangedEventHandler PropertyChanged;
        public int Revision { get; private set; }

        public FaviconService(IDatabase database, ILogger logger = null)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _logger = logger;
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

            return new UriBuilder(uri) { Fragment = string.Empty }.Uri;
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

        public string GetIconUrl(string pageUrl)
        {
            VerifyAccess();
            var uri = GetPageUri(pageUrl);
            return _initialized && uri != null && _icons.TryGetValue(uri.AbsoluteUri, out var iconUrl) ? iconUrl : null;
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
                _work = PersistAsync(_work, item.Key, item.Value);

            return _initializeTask = PublishAsync(loadTask);
        }

        private async Task EnsureSchemaAsync()
        {
            if (_schemaReady)
                return;

            await _database.TransactionAsync(session =>
            {
                long version = Convert.ToInt64(session.Scalar("PRAGMA user_version"));

                if (version > 1)
                    throw new InvalidOperationException("The browser database version is not supported.");

                if (version == 0)
                {
                    session.Execute(@"CREATE TABLE IF NOT EXISTS website_icons (
                        page_url TEXT PRIMARY KEY NOT NULL,
                        icon_url TEXT NOT NULL,
                        created_at INTEGER NOT NULL,
                        updated_at INTEGER NOT NULL,
                        deleted_at INTEGER NULL)");
                    session.Execute("PRAGMA user_version = 1");
                }
            }).ConfigureAwait(false);

            _schemaReady = true;
        }

        private async Task<List<KeyValuePair<string, string>>> LoadAsync()
        {
            try
            {
                await EnsureSchemaAsync().ConfigureAwait(false);

                return await _database.QueryAsync(
                    "SELECT page_url, icon_url FROM website_icons WHERE deleted_at IS NULL",
                    row => new KeyValuePair<string, string>(row.GetString(0), row.GetString(1))).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                LogError("Failed to load favicon mappings", e);
                return new List<KeyValuePair<string, string>>();
            }
        }

        private async Task PublishAsync(Task<List<KeyValuePair<string, string>>> loadTask)
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
                            var pageUri = GetPageUri(item.Key);
                            var iconUrl = pageUri == null ? null : GetAbsoluteIconUrl(pageUri, item.Value);
                            if (iconUrl != null)
                                _icons[pageUri.AbsoluteUri] = iconUrl;
                        }

                        foreach (var item in _pending)
                            _icons[item.Key] = item.Value;

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

            string key = pageUri.AbsoluteUri;
            bool changed = false;

            if (!_initialized)
            {
                _pending.Add(new KeyValuePair<string, string>(key, absoluteIconUrl));
            }
            else if (!_icons.TryGetValue(key, out var current) || current != absoluteIconUrl)
            {
                _icons[key] = absoluteIconUrl;
                changed = true;
            }

            // Repeated discoveries still reach SQL so a failed write can be retried.
            // The conditional upsert preserves timestamps for unchanged mappings.
            var work = _initializeTask == null ? Task.CompletedTask
                : _work = PersistAsync(_work, key, absoluteIconUrl);

            // A change notification may synchronously report another discovery.
            // Queue this write first so reentrant discoveries retain their order.
            if (changed)
                NotifyChanged();

            return work;
        }

        private async Task PersistAsync(Task previous, string pageUrl, string iconUrl)
        {
            await previous.ConfigureAwait(false);
            try
            {
                await EnsureSchemaAsync().ConfigureAwait(false);
                await _database.ExecuteAsync(@"INSERT INTO website_icons
                    (page_url, icon_url, created_at, updated_at, deleted_at)
                    VALUES (@page, @icon, @now, @now, NULL)
                    ON CONFLICT(page_url) DO UPDATE SET
                        icon_url = excluded.icon_url,
                        updated_at = excluded.updated_at,
                        deleted_at = NULL
                    WHERE website_icons.icon_url <> excluded.icon_url OR website_icons.deleted_at IS NOT NULL",
                    new Dictionary<string, object>
                    {
                        ["@page"] = pageUrl,
                        ["@icon"] = iconUrl,
                        ["@now"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    }).ConfigureAwait(false);
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
    }
}
