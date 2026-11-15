using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Permissions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services.UserScripts.Persistence
{
    public sealed class SqliteUserScriptPermissionDecisionStore : IUserScriptPermissionDecisionStore
    {
        private readonly UserScriptStorage _storage;

        public SqliteUserScriptPermissionDecisionStore(UserScriptStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        }

        public Task<PermissionDecision?> GetAsync(UserScriptPermissionScope scope, CancellationToken token)
        {
            var parameters = Parameters(scope);
            return _storage.RunAsync<PermissionDecision?>(async () =>
            {
                PermissionDecision? result = null;
                await _storage.Database.TransactionAsync(session =>
                {
                    token.ThrowIfCancellationRequested();
                    var value = session.Scalar("SELECT decision FROM monkeysharp_permission_decisions WHERE script_id = @id AND kind = @kind AND target = @target", parameters);
                    if (value != null && value != DBNull.Value)
                    {
                        var encoded = Convert.ToInt64(value);
                        if (encoded != 1 && encoded != 2)
                            throw new InvalidOperationException("Invalid remembered permission decision.");
                        result = encoded == 1 ? PermissionDecision.Allow : PermissionDecision.Deny;
                    }
                }).ConfigureAwait(false);
                return result;
            }, token);
        }

        public Task SetAsync(UserScriptPermissionScope scope, PermissionDecision decision, CancellationToken token)
        {
            if (decision != PermissionDecision.Allow && decision != PermissionDecision.Deny)
                throw new ArgumentException("Only terminal permission decisions may be stored.", nameof(decision));

            var parameters = Parameters(scope);
            parameters["@decision"] = decision == PermissionDecision.Allow ? 1 : 2;
            parameters["@updated"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            return _storage.RunAsync(() => _storage.Database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                if (Convert.ToInt64(session.Scalar("SELECT COUNT(*) FROM monkeysharp_scripts WHERE script_id = @id", parameters)) == 0)
                    throw new KeyNotFoundException("The script installation has been removed.");
                session.Execute(@"INSERT INTO monkeysharp_permission_decisions (script_id, kind, target, decision, updated_at)
                    VALUES (@id, @kind, @target, @decision, @updated)
                    ON CONFLICT(script_id, kind, target) DO UPDATE SET decision = excluded.decision, updated_at = excluded.updated_at", parameters);
                token.ThrowIfCancellationRequested();
            }), token);
        }

        public Task RevokeAsync(string scriptKey, CancellationToken token)
        {
            var id = scriptKey == null ? null : ScriptKey.Parse(scriptKey).ToString();
            return _storage.RunAsync(() => _storage.Database.TransactionAsync(session =>
            {
                token.ThrowIfCancellationRequested();
                session.Execute(id == null ? "DELETE FROM monkeysharp_permission_decisions"
                    : "DELETE FROM monkeysharp_permission_decisions WHERE script_id = @id", new Dictionary<string, object> { ["@id"] = id });
                token.ThrowIfCancellationRequested();
            }), token);
        }

        private static Dictionary<string, object> Parameters(UserScriptPermissionScope scope)
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
