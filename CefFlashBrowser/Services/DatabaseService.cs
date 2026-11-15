using CefFlashBrowser.Database;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.UserScripts;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Permissions;
using Mzying2001.MonkeySharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    // The application owns the injected database and closes it after its consumers stop.
    public class DatabaseService
    {
        private const string SelectUserScriptSql =
            "SELECT script_id, source_origin, is_enabled, installed_at, updated_at, " +
            "source_sha256, prepared_revision_json FROM monkeysharp_scripts";

        private readonly IDatabase _database;
        private readonly object _lock = new object();
        private Task _initializeTask;

        public DatabaseService(IDatabase database)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
        }

        public Task InitializeAsync()
        {
            lock (_lock)
            {
                // Share in-flight and successful initialization, but allow failures to retry.
                if (_initializeTask == null || _initializeTask.IsFaulted || _initializeTask.IsCanceled)
                    _initializeTask = InitializeCoreAsync();

                return _initializeTask;
            }
        }

        private async Task InitializeCoreAsync()
        {
            await _database.TransactionAsync(session =>
            {
                long version = Convert.ToInt64(session.Scalar("PRAGMA user_version"));

                if (version > 2)
                    throw new InvalidOperationException("The browser database version is not supported.");

                if (version == 0)
                {
                    session.Execute(
                        "CREATE TABLE IF NOT EXISTS website_icons ( " +
                        "page_url TEXT PRIMARY KEY NOT NULL, " +
                        "icon_url TEXT NOT NULL, " +
                        "updated_at INTEGER NOT NULL)");
                    session.Execute("PRAGMA user_version = 1");
                    version = 1;
                }

                if (version == 1)
                {
                    session.Execute(
                        "CREATE TABLE monkeysharp_scripts ( " +
                        "script_id TEXT PRIMARY KEY NOT NULL, " +
                        "install_order INTEGER NOT NULL UNIQUE, " +
                        "source_origin TEXT NULL, " +
                        "is_enabled INTEGER NOT NULL, " +
                        "installed_at TEXT NOT NULL, " +
                        "updated_at TEXT NOT NULL, " +
                        "source_sha256 TEXT NOT NULL, " +
                        "prepared_revision_json TEXT NOT NULL, " +
                        "display_name TEXT NULL)");

                    session.Execute(
                        "CREATE TABLE monkeysharp_values ( " +
                        "script_id TEXT NOT NULL, " +
                        "value_key TEXT COLLATE BINARY NOT NULL, " +
                        "encoded_value TEXT NOT NULL, " +
                        "value_order INTEGER NOT NULL, " +
                        "updated_at TEXT NOT NULL, " +
                        "PRIMARY KEY (script_id, value_key), " +
                        "UNIQUE (script_id, value_order))");

                    session.Execute(
                        "CREATE TABLE monkeysharp_permission_decisions ( " +
                        "script_id TEXT NOT NULL, " +
                        "kind TEXT NOT NULL, " +
                        "target TEXT NOT NULL, " +
                        "decision INTEGER NOT NULL, " +
                        "updated_at TEXT NOT NULL, " +
                        "PRIMARY KEY (script_id, kind, target))");

                    session.Execute(
                        "CREATE TABLE monkeysharp_tab_states ( " +
                        "script_id TEXT NOT NULL, " +
                        "tab_id TEXT NOT NULL, " +
                        "json_value TEXT NOT NULL, " +
                        "updated_at TEXT NOT NULL, " +
                        "PRIMARY KEY (script_id, tab_id))");
                    session.Execute("PRAGMA user_version = 2");
                }
            }).ConfigureAwait(false);
        }

        public async Task<List<WebsiteIconRecord>> GetWebsiteIconsAsync()
        {
            await InitializeAsync().ConfigureAwait(false);

            return await _database.QueryAsync(
                "SELECT page_url, icon_url, updated_at FROM website_icons ORDER BY updated_at, page_url DESC",
                row => new WebsiteIconRecord(row.GetString(0), row.GetString(1), row.GetInt64(2))).ConfigureAwait(false);
        }

        // Callers supply normalized URLs; URL matching and validation belong to the consumer.
        public async Task SaveWebsiteIconAsync(WebsiteIconRecord icon)
        {
            if (icon == null)
                throw new ArgumentNullException(nameof(icon));

            await InitializeAsync().ConfigureAwait(false);

            await _database.ExecuteAsync(
                "INSERT INTO website_icons (page_url, icon_url, updated_at) " +
                "VALUES (@page, @icon, @now) " +
                "ON CONFLICT(page_url) DO UPDATE SET " +
                "icon_url = excluded.icon_url, " +
                "updated_at = excluded.updated_at " +
                "WHERE website_icons.icon_url <> excluded.icon_url",
                new Dictionary<string, object>
                {
                    ["@page"] = icon.PageUrl,
                    ["@icon"] = icon.IconUrl,
                    ["@now"] = icon.UpdatedAt
                }).ConfigureAwait(false);
        }

        public async Task<List<UserScriptDatabaseRecord>> GetUserScriptsAsync()
        {
            await InitializeAsync().ConfigureAwait(false);

            return await _database.QueryAsync(SelectUserScriptSql + " ORDER BY install_order",
                ReadUserScript).ConfigureAwait(false);
        }

        public async Task<UserScriptDatabaseRecord> GetUserScriptAsync(ScriptKey key)
        {
            await InitializeAsync().ConfigureAwait(false);

            var rows = await _database.QueryAsync(SelectUserScriptSql + " WHERE script_id = @id",
                ReadUserScript, new Dictionary<string, object> { ["@id"] = key.ToString() }).ConfigureAwait(false);
            return rows.SingleOrDefault();
        }

        // The callback switches source files after the row update and before commit.
        // It must be synchronous and must not enter another database or storage operation.
        public async Task SaveUserScriptAsync(UserScriptDatabaseRecord record, Action commitSource, CancellationToken token)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                session.Execute(
                    "INSERT INTO monkeysharp_scripts " +
                    "(script_id, install_order, source_origin, is_enabled, installed_at, updated_at, source_sha256, prepared_revision_json) " +
                    "VALUES (@id, (SELECT COALESCE(MAX(install_order), -1) + 1 FROM monkeysharp_scripts), " +
                    "@origin, @enabled, @installed, @updated, @hash, @prepared) " +
                    "ON CONFLICT(script_id) DO UPDATE SET source_origin = excluded.source_origin, " +
                    "is_enabled = excluded.is_enabled, updated_at = excluded.updated_at, " +
                    "source_sha256 = excluded.source_sha256, prepared_revision_json = excluded.prepared_revision_json",
                    new Dictionary<string, object>
                    {
                        ["@id"] = record.ScriptKey.ToString(),
                        ["@origin"] = record.SourceOrigin,
                        ["@enabled"] = record.IsEnabled ? 1 : 0,
                        ["@installed"] = record.InstalledAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                        ["@updated"] = record.UpdatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                        ["@hash"] = record.SourceSha256,
                        ["@prepared"] = record.PreparedRevisionJson
                    });

                token.ThrowIfCancellationRequested();
                commitSource?.Invoke();
                // No cancellation checks after the file switch: commit is next.
            }).ConfigureAwait(false);
        }

        // Uses the same synchronous file-switch contract as SaveUserScriptAsync.
        public async Task DeleteUserScriptAsync(ScriptKey key, Action commitSource, CancellationToken token)
        {
            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                var parameters = new Dictionary<string, object> { ["@id"] = key.ToString() };
                session.Execute("DELETE FROM monkeysharp_values WHERE script_id = @id", parameters);
                session.Execute("DELETE FROM monkeysharp_permission_decisions WHERE script_id = @id", parameters);
                session.Execute("DELETE FROM monkeysharp_tab_states WHERE script_id = @id", parameters);
                session.Execute("DELETE FROM monkeysharp_scripts WHERE script_id = @id", parameters);

                token.ThrowIfCancellationRequested();
                commitSource?.Invoke();
            }).ConfigureAwait(false);
        }

        public async Task<StoredValue> GetUserScriptValueAsync(string scriptId, string key, CancellationToken token)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            await InitializeAsync().ConfigureAwait(false);

            var value = StoredValue.Missing;
            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                value = ReadUserScriptValue(session, scriptId, key);
            }).ConfigureAwait(false);
            return value;
        }

        public async Task<List<KeyValuePair<string, UserScriptValue>>> GetUserScriptValuesAsync(
            string scriptId, CancellationToken token)
        {
            await InitializeAsync().ConfigureAwait(false);

            List<KeyValuePair<string, UserScriptValue>> values = null;
            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                values = session.Query(
                    "SELECT value_key, encoded_value FROM monkeysharp_values " +
                    "WHERE script_id = @id ORDER BY value_order",
                    row => new KeyValuePair<string, UserScriptValue>(row.GetString(0), UserScriptValue.FromEncoded(row.GetString(1))),
                    new Dictionary<string, object> { ["@id"] = scriptId });
            }).ConfigureAwait(false);
            return values;
        }

        public async Task<IReadOnlyList<UserScriptValueDatabaseChange>> ApplyUserScriptValuesAsync(
            string scriptId, IEnumerable<UserScriptValueMutation> mutations, CancellationToken token)
        {
            var batch = UserScriptValueMutation.Validate(mutations);
            await InitializeAsync().ConfigureAwait(false);

            var changes = new List<UserScriptValueDatabaseChange>();
            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                EnsureUserScriptExists(session, scriptId);

                foreach (var mutation in batch)
                {
                    var old = ReadUserScriptValue(session, scriptId, mutation.Key);
                    var value = mutation.Value;
                    if (value == null && !old.Exists || value != null && old.Exists && old.Value.Equals(value))
                        continue;

                    var parameters = new Dictionary<string, object>
                    {
                        ["@id"] = scriptId,
                        ["@key"] = mutation.Key,
                        ["@value"] = value?.EncodedValue,
                        ["@updated"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                    };
                    if (value == null)
                    {
                        session.Execute("DELETE FROM monkeysharp_values WHERE script_id = @id AND value_key = @key", parameters);
                    }
                    else
                    {
                        session.Execute(
                            "INSERT INTO monkeysharp_values (script_id, value_key, encoded_value, value_order, updated_at) " +
                            "VALUES (@id, @key, @value, " +
                            "(SELECT COALESCE(MAX(value_order), -1) + 1 FROM monkeysharp_values WHERE script_id = @id), @updated) " +
                            "ON CONFLICT(script_id, value_key) DO UPDATE SET " +
                            "encoded_value = excluded.encoded_value, updated_at = excluded.updated_at", parameters);
                    }

                    changes.Add(new UserScriptValueDatabaseChange(mutation.Key, old,
                        value == null ? StoredValue.Missing : new StoredValue(true, value)));
                }

                token.ThrowIfCancellationRequested();
            }).ConfigureAwait(false);
            return changes.AsReadOnly();
        }

        public async Task<PermissionDecision?> GetUserScriptPermissionDecisionAsync(
            UserScriptPermissionScope scope, CancellationToken token)
        {
            var parameters = UserScriptPermissionParameters(scope);
            await InitializeAsync().ConfigureAwait(false);

            PermissionDecision? result = null;
            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                var value = session.Scalar(
                    "SELECT decision FROM monkeysharp_permission_decisions " +
                    "WHERE script_id = @id AND kind = @kind AND target = @target", parameters);
                if (value != null && value != DBNull.Value)
                {
                    var encoded = Convert.ToInt64(value);
                    if (encoded != 1 && encoded != 2)
                        throw new InvalidOperationException("Invalid remembered permission decision.");

                    result = encoded == 1 ? PermissionDecision.Allow : PermissionDecision.Deny;
                }
            }).ConfigureAwait(false);
            return result;
        }

        public async Task SaveUserScriptPermissionDecisionAsync(
            UserScriptPermissionScope scope, PermissionDecision decision, CancellationToken token)
        {
            if (decision != PermissionDecision.Allow && decision != PermissionDecision.Deny)
                throw new ArgumentException("Only terminal permission decisions may be stored.", nameof(decision));

            var parameters = UserScriptPermissionParameters(scope);
            parameters["@decision"] = decision == PermissionDecision.Allow ? 1 : 2;
            parameters["@updated"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                EnsureUserScriptExists(session, ScriptKey.Parse(scope.ScriptKey).ToString());
                session.Execute(
                    "INSERT INTO monkeysharp_permission_decisions (script_id, kind, target, decision, updated_at) " +
                    "VALUES (@id, @kind, @target, @decision, @updated) " +
                    "ON CONFLICT(script_id, kind, target) DO UPDATE SET " +
                    "decision = excluded.decision, updated_at = excluded.updated_at", parameters);
                token.ThrowIfCancellationRequested();
            }).ConfigureAwait(false);
        }

        public async Task RevokeUserScriptPermissionsAsync(string scriptId, CancellationToken token)
        {
            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                session.Execute(scriptId == null ? "DELETE FROM monkeysharp_permission_decisions"
                    : "DELETE FROM monkeysharp_permission_decisions WHERE script_id = @id",
                    new Dictionary<string, object> { ["@id"] = scriptId });
                token.ThrowIfCancellationRequested();
            }).ConfigureAwait(false);
        }

        public async Task SaveUserScriptTabStateAsync(string scriptId, string tabId, string jsonValue,
            Action ensureAlive, CancellationToken token)
        {
            if (ensureAlive == null)
                throw new ArgumentNullException(nameof(ensureAlive));

            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                // Check on the database worker: a tab may close while this transaction waits in the queue.
                ensureAlive();

                var parameters = new Dictionary<string, object>
                {
                    ["@id"] = scriptId,
                    ["@tab"] = tabId,
                    ["@value"] = jsonValue,
                    ["@updated"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)
                };
                EnsureUserScriptExists(session, scriptId);
                session.Execute(
                    "INSERT INTO monkeysharp_tab_states (script_id, tab_id, json_value, updated_at) " +
                    "VALUES (@id, @tab, @value, @updated) " +
                    "ON CONFLICT(script_id, tab_id) DO UPDATE SET " +
                    "json_value = excluded.json_value, updated_at = excluded.updated_at", parameters);
                token.ThrowIfCancellationRequested();
            }).ConfigureAwait(false);
        }

        public async Task<IReadOnlyDictionary<string, string>> GetUserScriptTabStatesAsync(
            string scriptId, CancellationToken token)
        {
            await InitializeAsync().ConfigureAwait(false);

            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                foreach (var row in session.Query(
                    "SELECT tab_id, json_value FROM monkeysharp_tab_states WHERE script_id = @id ORDER BY tab_id",
                    item => new KeyValuePair<string, string>(item.GetString(0), item.GetString(1)),
                    new Dictionary<string, object> { ["@id"] = scriptId }))
                    values.Add(row.Key, row.Value);
            }).ConfigureAwait(false);
            return new ReadOnlyDictionary<string, string>(values);
        }

        // A null tab ID clears state left by a previous process during runtime initialization.
        public async Task DeleteUserScriptTabStatesAsync(string tabId, CancellationToken token)
        {
            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                session.Execute(tabId == null ? "DELETE FROM monkeysharp_tab_states"
                    : "DELETE FROM monkeysharp_tab_states WHERE tab_id = @tab",
                    new Dictionary<string, object> { ["@tab"] = tabId });
            }).ConfigureAwait(false);
        }

        private static UserScriptDatabaseRecord ReadUserScript(IDataRecord row)
        {
            return new UserScriptDatabaseRecord(ScriptKey.Parse(row.GetString(0)),
                row.IsDBNull(1) ? null : row.GetString(1), row.GetInt64(2) != 0,
                DateTimeOffset.ParseExact(row.GetString(3), "O", CultureInfo.InvariantCulture),
                DateTimeOffset.ParseExact(row.GetString(4), "O", CultureInfo.InvariantCulture),
                row.GetString(5), row.IsDBNull(6) ? null : row.GetString(6));
        }

        private static void EnsureUserScriptExists(IDatabaseSession session, string scriptId)
        {
            if (Convert.ToInt64(session.Scalar("SELECT COUNT(*) FROM monkeysharp_scripts WHERE script_id = @id",
                new Dictionary<string, object> { ["@id"] = scriptId })) == 0)
                throw new KeyNotFoundException("The script installation has been removed.");
        }

        private static StoredValue ReadUserScriptValue(IDatabaseSession session, string scriptId, string key)
        {
            var encoded = session.Scalar("SELECT encoded_value FROM monkeysharp_values WHERE script_id = @id AND value_key = @key",
                new Dictionary<string, object>
                {
                    ["@id"] = scriptId,
                    ["@key"] = key
                });
            return encoded == null || encoded == DBNull.Value ? StoredValue.Missing
                : new StoredValue(true, UserScriptValue.FromEncoded((string)encoded));
        }

        private static Dictionary<string, object> UserScriptPermissionParameters(UserScriptPermissionScope scope)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            return new Dictionary<string, object>
            {
                ["@id"] = ScriptKey.Parse(scope.ScriptKey).ToString(),
                ["@kind"] = scope.Kind,
                ["@target"] = scope.Target
            };
        }
    }
}
