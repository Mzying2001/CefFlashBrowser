using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Permissions;
using System;
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
            ValidateScope(scope);
            return _storage.RunAsync(() => _storage.Database.GetUserScriptPermissionDecisionAsync(scope, token), token);
        }

        public Task SetAsync(UserScriptPermissionScope scope, PermissionDecision decision, CancellationToken token)
        {
            if (decision != PermissionDecision.Allow && decision != PermissionDecision.Deny)
                throw new ArgumentException("Only terminal permission decisions may be stored.", nameof(decision));

            ValidateScope(scope);
            return _storage.RunAsync(() => _storage.Database.SaveUserScriptPermissionDecisionAsync(scope, decision, token), token);
        }

        public Task RevokeAsync(string scriptKey, CancellationToken token)
        {
            var id = scriptKey == null ? null : ScriptKey.Parse(scriptKey).ToString();
            return _storage.RunAsync(() => _storage.Database.RevokeUserScriptPermissionsAsync(id, token), token);
        }

        private static void ValidateScope(UserScriptPermissionScope scope)
        {
            if (scope == null)
                throw new ArgumentNullException(nameof(scope));

            ScriptKey.Parse(scope.ScriptKey);
        }
    }
}
