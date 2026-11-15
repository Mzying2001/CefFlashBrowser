using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts.Persistence
{
    public sealed class SqliteUserScriptTabStateService : ITabStateService
    {
        private readonly UserScriptStorage _storage;
        private readonly string _tabId;
        private readonly object _lock = new object();
        private volatile bool _alive = true;
        private Task _close;

        public SqliteUserScriptTabStateService(UserScriptStorage storage, string tabId)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _tabId = !string.IsNullOrEmpty(tabId) ? tabId : throw new ArgumentException("A stable tab ID is required.", nameof(tabId));
        }

        public async Task<string> GetAsync(ScriptKey key, DocumentFrame frame, CancellationToken token)
        {
            var states = await GetAllAsync(key, token).ConfigureAwait(false);
            return states.TryGetValue(_tabId, out var value) ? value : "{}";
        }

        public Task SaveAsync(ScriptKey key, DocumentFrame frame, string jsonValue, CancellationToken token)
        {
            var id = ScriptKey.Parse(key.ToString()).ToString();
            using (var document = JsonDocument.Parse(jsonValue))
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("Tab state must be a JSON object.", nameof(jsonValue));
            return _storage.RunAsync(() => _storage.Database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                if (!_alive) throw new OperationCanceledException("The tab has closed.");
                var parameters = new Dictionary<string, object>
                {
                    ["@id"] = id, ["@tab"] = _tabId, ["@value"] = jsonValue,
                    ["@updated"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };
                if (Convert.ToInt64(session.Scalar("SELECT COUNT(*) FROM monkeysharp_scripts WHERE script_id = @id", parameters)) == 0)
                    throw new KeyNotFoundException("The script installation has been removed.");
                session.Execute(@"INSERT INTO monkeysharp_tab_states (script_id, tab_id, json_value, updated_at)
                    VALUES (@id, @tab, @value, @updated)
                    ON CONFLICT(script_id, tab_id) DO UPDATE SET json_value = excluded.json_value, updated_at = excluded.updated_at", parameters);
                token.ThrowIfCancellationRequested();
            }), token);
        }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(ScriptKey key, CancellationToken token)
        {
            var id = ScriptKey.Parse(key.ToString()).ToString();
            return _storage.RunAsync<IReadOnlyDictionary<string, string>>(async () =>
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                await _storage.Database.TransactionAsync(session =>
                {
                    token.ThrowIfCancellationRequested();
                    foreach (var row in session.Query("SELECT tab_id, json_value FROM monkeysharp_tab_states WHERE script_id = @id ORDER BY tab_id",
                        item => new KeyValuePair<string, string>(item.GetString(0), item.GetString(1)),
                        new Dictionary<string, object> { ["@id"] = id }))
                        values.Add(row.Key, row.Value);
                }).ConfigureAwait(false);
                return new ReadOnlyDictionary<string, string>(values);
            }, token);
        }

        public Task CloseAsync()
        {
            lock (_lock)
            {
                if (_close != null) return _close;
                _alive = false;
                return _close = _storage.RunAsync(() => _storage.Database.ExecuteAsync(
                    "DELETE FROM monkeysharp_tab_states WHERE tab_id = @tab", new Dictionary<string, object> { ["@tab"] = _tabId }), CancellationToken.None);
            }
        }
    }
}
