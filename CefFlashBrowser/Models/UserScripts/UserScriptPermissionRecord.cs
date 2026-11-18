using Mzying2001.MonkeySharp.Core.Permissions;

namespace CefFlashBrowser.Models.UserScripts
{
    public sealed class UserScriptPermissionRecord
    {
        public UserScriptPermissionScope Scope { get; }
        public PermissionDecision Decision { get; }
        public long UpdatedAt { get; }

        internal UserScriptPermissionRecord(UserScriptPermissionScope scope, PermissionDecision decision, long updatedAt)
        {
            Scope = scope;
            Decision = decision;
            UpdatedAt = updatedAt;
        }
    }

    public sealed class UserScriptPagePermission
    {
        public string ScriptName { get; }
        public bool IsScriptDisabled { get; }
        public UserScriptPermissionRecord Record { get; }

        internal UserScriptPagePermission(string scriptName, bool isScriptDisabled, UserScriptPermissionRecord record)
        {
            ScriptName = scriptName;
            IsScriptDisabled = isScriptDisabled;
            Record = record;
        }
    }
}
