using CefFlashBrowser.Database;
using CefFlashBrowser.Models.UserScripts;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Permissions;
using Mzying2001.MonkeySharp.Core.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    public partial class DatabaseService
    {
        private const string SelectUserScriptSql =
            "SELECT script_id, source_origin, is_enabled, installed_at, updated_at, " +
            "source_sha256, commit_id FROM monkeysharp_scripts";

        private static void CreateUserScriptTables(IDatabaseSession session)
        {
            session.Execute(
                "CREATE TABLE IF NOT EXISTS monkeysharp_scripts ( " +
                "script_id TEXT PRIMARY KEY NOT NULL, " +
                "install_order INTEGER NOT NULL UNIQUE, " +
                "source_origin TEXT NULL, " +
                "is_enabled INTEGER NOT NULL CHECK (is_enabled IN (0, 1)), " +
                "installed_at INTEGER NOT NULL, " +
                "updated_at INTEGER NOT NULL, " +
                "source_sha256 TEXT NOT NULL, " +
                "commit_id TEXT NOT NULL)");

            session.Execute(
                "CREATE TABLE IF NOT EXISTS monkeysharp_script_assets ( " +
                "script_id TEXT NOT NULL, " +
                "kind INTEGER NOT NULL CHECK (kind IN (0, 1)), " +
                "ordinal INTEGER NOT NULL CHECK (ordinal >= 0), " +
                "name TEXT NULL, " +
                "url TEXT NOT NULL, " +
                "content_sha256 TEXT NOT NULL, " +
                "mime TEXT NOT NULL, " +
                "charset TEXT NULL, " +
                "PRIMARY KEY (script_id, kind, ordinal), " +
                "CHECK ((kind = 0 AND name IS NULL) OR (kind = 1 AND name IS NOT NULL AND length(name) > 0)))");

            session.Execute(
                "CREATE UNIQUE INDEX IF NOT EXISTS monkeysharp_resource_names ON monkeysharp_script_assets(script_id, name) WHERE kind = 1");

            session.Execute(
                "CREATE INDEX IF NOT EXISTS monkeysharp_asset_content ON monkeysharp_script_assets(content_sha256)");

            session.Execute(
                "CREATE TABLE IF NOT EXISTS monkeysharp_values ( " +
                "script_id TEXT NOT NULL, " +
                "value_key TEXT COLLATE BINARY NOT NULL, " +
                "encoded_value TEXT NOT NULL, " +
                "value_order INTEGER NOT NULL, " +
                "updated_at INTEGER NOT NULL, " +
                "PRIMARY KEY (script_id, value_key), " +
                "UNIQUE (script_id, value_order))");

            session.Execute(
                "CREATE TABLE IF NOT EXISTS monkeysharp_permission_decisions ( " +
                "script_id TEXT NOT NULL, " +
                "kind TEXT NOT NULL, " +
                "target TEXT NOT NULL, " +
                "decision INTEGER NOT NULL, " +
                "updated_at INTEGER NOT NULL, " +
                "PRIMARY KEY (script_id, kind, target))");

            session.Execute(
                "CREATE TABLE IF NOT EXISTS monkeysharp_tab_states ( " +
                "script_id TEXT NOT NULL, " +
                "tab_id TEXT NOT NULL, " +
                "json_value TEXT NOT NULL, " +
                "updated_at INTEGER NOT NULL, " +
                "PRIMARY KEY (script_id, tab_id))");
        }

        private static void AddUserScriptSortOrderColumn(IDatabaseSession session)
        {
            session.Execute("ALTER TABLE monkeysharp_scripts ADD COLUMN sort_order INTEGER");
        }

        public async Task<List<UserScriptDatabaseRecord>> GetUserScriptsAsync()
        {
            await InitializeAsync().ConfigureAwait(false);

            return await _database.QueryAsync(SelectUserScriptSql + " ORDER BY sort_order IS NULL, sort_order, install_order",
                ReadUserScript).ConfigureAwait(false);
        }

        internal async Task SetUserScriptOrderAsync(IReadOnlyList<ScriptKey> keys, CancellationToken token)
        {
            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();

                if (Convert.ToInt64(session.Scalar("SELECT COUNT(*) FROM monkeysharp_scripts")) != keys.Count)
                    throw new DBConcurrencyException("The installed scripts changed while saving their order.");

                for (var i = 0; i < keys.Count; i++)
                {
                    token.ThrowIfCancellationRequested();

                    var affected = session.Execute("UPDATE monkeysharp_scripts SET sort_order = @rank WHERE script_id = @id",
                        new Dictionary<string, object> { ["@rank"] = i, ["@id"] = keys[i].ToString() });

                    if (affected != 1)
                        throw new DBConcurrencyException("The installed scripts changed while saving their order.");
                }

                token.ThrowIfCancellationRequested();
            }).ConfigureAwait(false);
        }

        public async Task<UserScriptDatabaseRecord> GetUserScriptAsync(ScriptKey key)
        {
            await InitializeAsync().ConfigureAwait(false);

            var rows = await _database.QueryAsync(SelectUserScriptSql + " WHERE script_id = @id",
                ReadUserScript, new Dictionary<string, object> { ["@id"] = key.ToString() }).ConfigureAwait(false);

            return rows.SingleOrDefault();
        }

        public async Task<List<UserScriptAssetDatabaseRecord>> GetUserScriptAssetsAsync(ScriptKey? key = null)
        {
            await InitializeAsync().ConfigureAwait(false);

            return await _database.QueryAsync(
                "SELECT script_id, kind, ordinal, name, url, content_sha256, mime, charset FROM monkeysharp_script_assets" +
                (key.HasValue ? " WHERE script_id = @id" : "") + " ORDER BY script_id, kind, ordinal",
                row => new UserScriptAssetDatabaseRecord(row.GetString(0), row.GetInt32(1), row.GetInt32(2),
                    row.IsDBNull(3) ? null : row.GetString(3), row.GetString(4), row.GetString(5), row.GetString(6),
                    row.IsDBNull(7) ? null : row.GetString(7)),
                new Dictionary<string, object> { ["@id"] = key?.ToString() }).ConfigureAwait(false);
        }

        public async Task SetUserScriptEnabledAsync(ScriptKey key, bool enabled, DateTimeOffset updatedAt, CancellationToken token)
        {
            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                EnsureUserScriptExists(session, key.ToString());

                session.Execute("UPDATE monkeysharp_scripts SET is_enabled = @enabled, updated_at = @updated WHERE script_id = @id",
                    new Dictionary<string, object>
                    {
                        ["@id"] = key.ToString(),
                        ["@enabled"] = enabled ? 1 : 0,
                        ["@updated"] = updatedAt.ToUnixTimeMilliseconds()
                    });
            }).ConfigureAwait(false);
        }

        // The callback switches source files after the row update and before commit.
        // It must be synchronous and must not enter another database or storage operation.
        public async Task SaveUserScriptAsync(UserScriptDatabaseRecord record, IReadOnlyList<UserScriptAssetDatabaseRecord> assets,
            Action commitSource, CancellationToken token)
        {
            if (record == null)
                throw new ArgumentNullException(nameof(record));

            if (assets == null || assets.Any(asset => asset == null || asset.ScriptId != record.ScriptKey.ToString()))
                throw new ArgumentException("Assets must belong to the installation.", nameof(assets));

            await InitializeAsync().ConfigureAwait(false);

            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                session.Execute(
                    "INSERT INTO monkeysharp_scripts " +
                    "(script_id, install_order, source_origin, is_enabled, installed_at, updated_at, source_sha256, commit_id) " +
                    "VALUES (@id, (SELECT COALESCE(MAX(install_order), -1) + 1 FROM monkeysharp_scripts), " +
                    "@origin, @enabled, @installed, @updated, @hash, @commit) " +
                    "ON CONFLICT(script_id) DO UPDATE SET source_origin = excluded.source_origin, " +
                    "is_enabled = excluded.is_enabled, updated_at = excluded.updated_at, " +
                    "source_sha256 = excluded.source_sha256, commit_id = excluded.commit_id",
                    new Dictionary<string, object>
                    {
                        ["@id"] = record.ScriptKey.ToString(),
                        ["@origin"] = record.SourceOrigin,
                        ["@enabled"] = record.IsEnabled ? 1 : 0,
                        ["@installed"] = record.InstalledAt.ToUnixTimeMilliseconds(),
                        ["@updated"] = record.UpdatedAt.ToUnixTimeMilliseconds(),
                        ["@hash"] = record.SourceSha256,
                        ["@commit"] = record.CommitId
                    });

                var parameters = new Dictionary<string, object> { ["@id"] = record.ScriptKey.ToString() };

                session.Execute("DELETE FROM monkeysharp_script_assets WHERE script_id = @id", parameters);

                foreach (var asset in assets)
                    session.Execute(
                        "INSERT INTO monkeysharp_script_assets " +
                        "(script_id, kind, ordinal, name, url, content_sha256, mime, charset) " +
                        "VALUES (@id, @kind, @ordinal, @name, @url, @hash, @mime, @charset)",
                        new Dictionary<string, object>
                        {
                            ["@id"] = asset.ScriptId,
                            ["@kind"] = asset.Kind,
                            ["@ordinal"] = asset.Ordinal,
                            ["@name"] = asset.Name,
                            ["@url"] = asset.Url,
                            ["@hash"] = asset.ContentSha256,
                            ["@mime"] = asset.Mime,
                            ["@charset"] = asset.Charset
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

                session.Execute("DELETE FROM monkeysharp_script_assets WHERE script_id = @id", parameters);
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
                        ["@updated"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
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
                    result = DecodePermissionDecision(Convert.ToInt64(value));
            }).ConfigureAwait(false);

            return result;
        }

        public async Task<IReadOnlyList<UserScriptPermissionRecord>> GetUserScriptPermissionsAsync(
            IReadOnlyCollection<ScriptKey> scriptKeys, CancellationToken token)
        {
            await InitializeAsync().ConfigureAwait(false);

            var records = new List<UserScriptPermissionRecord>();
            await _database.TransactionAsync(session =>
            {
                foreach (var scriptKey in scriptKeys)
                {
                    token.ThrowIfCancellationRequested();
                    var id = scriptKey.ToString();
                    records.AddRange(session.Query(
                        "SELECT kind, target, decision, updated_at FROM monkeysharp_permission_decisions WHERE script_id = @id",
                        row => new UserScriptPermissionRecord(new UserScriptPermissionScope(id, row.GetString(0), row.GetString(1)),
                            DecodePermissionDecision(row.GetInt64(2)), row.GetInt64(3)),
                        new Dictionary<string, object> { ["@id"] = id }));
                }
            }).ConfigureAwait(false);

            return records.AsReadOnly();
        }

        public async Task SaveUserScriptPermissionDecisionAsync(
            UserScriptPermissionScope scope, PermissionDecision decision, CancellationToken token)
        {
            var parameters = UserScriptPermissionParameters(scope);

            parameters["@decision"] = EncodePermissionDecision(decision);
            parameters["@updated"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
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

        public async Task<bool> ChangeUserScriptPermissionAsync(
            UserScriptPermissionRecord expected, PermissionDecision? decision, CancellationToken token)
        {
            var parameters = UserScriptPermissionParameters(expected.Scope);
            parameters["@previousDecision"] = EncodePermissionDecision(expected.Decision);
            parameters["@previousUpdate"] = expected.UpdatedAt;

            var sql = "DELETE FROM monkeysharp_permission_decisions";
            if (decision.HasValue)
            {
                parameters["@decision"] = EncodePermissionDecision(decision.Value);
                parameters["@updated"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                // Keep fast edits distinct even when they share a clock tick.
                sql = "UPDATE monkeysharp_permission_decisions SET decision = @decision, updated_at = MAX(@updated, updated_at + 1)";
            }

            await InitializeAsync().ConfigureAwait(false);

            var changed = false;
            await _database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                changed = session.Execute(sql + " WHERE script_id = @id AND kind = @kind AND target = @target " +
                    "AND decision = @previousDecision AND updated_at = @previousUpdate", parameters) == 1;
                token.ThrowIfCancellationRequested();
            }).ConfigureAwait(false);

            return changed;
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
                    ["@updated"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
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
            var key = ScriptKey.Parse(row.GetString(0));

            if (key.ToString() != row.GetString(0))
                throw new FormatException("The installation directory contains a noncanonical script key.");

            return new UserScriptDatabaseRecord(key,
                row.IsDBNull(1) ? null : row.GetString(1), row.GetInt64(2) != 0,
                DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(3)),
                DateTimeOffset.FromUnixTimeMilliseconds(row.GetInt64(4)),
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

        private static int EncodePermissionDecision(PermissionDecision decision)
        {
            if (decision != PermissionDecision.Allow && decision != PermissionDecision.Deny)
                throw new ArgumentException("Only terminal permission decisions may be stored.", nameof(decision));

            return decision == PermissionDecision.Allow ? 1 : 2;
        }

        private static PermissionDecision DecodePermissionDecision(long encoded)
        {
            if (encoded != 1 && encoded != 2)
                throw new InvalidOperationException("Invalid remembered permission decision.");

            return encoded == 1 ? PermissionDecision.Allow : PermissionDecision.Deny;
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
