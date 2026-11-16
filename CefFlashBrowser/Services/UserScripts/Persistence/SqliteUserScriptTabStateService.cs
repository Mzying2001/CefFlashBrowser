using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Runtime;
using System;
using System.Collections.Generic;
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

            return _storage.RunAsync(() => _storage.Database.SaveUserScriptTabStateAsync(
                id, _tabId, jsonValue, EnsureAlive, token), token);
        }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(ScriptKey key, CancellationToken token)
        {
            var id = ScriptKey.Parse(key.ToString()).ToString();
            return _storage.RunAsync(() => _storage.Database.GetUserScriptTabStatesAsync(id, token), token);
        }

        public Task CloseAsync()
        {
            lock (_lock)
            {
                if (_close != null)
                    return _close;

                _alive = false;
                return _close = _storage.RunAsync(() => _storage.Database.DeleteUserScriptTabStatesAsync(
                    _tabId, CancellationToken.None), CancellationToken.None);
            }
        }

        private void EnsureAlive()
        {
            if (!_alive)
                throw new OperationCanceledException("The tab has closed.");
        }
    }
}
