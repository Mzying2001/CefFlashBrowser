using Mzying2001.MonkeySharp.Core.Domain;
using System;

namespace CefFlashBrowser.Models.UserScripts
{
    public sealed class UserScriptDatabaseRecord
    {
        public ScriptKey ScriptKey { get; }

        public string SourceOrigin { get; }

        public bool IsEnabled { get; }

        public DateTimeOffset InstalledAt { get; }

        public DateTimeOffset UpdatedAt { get; }

        public string SourceSha256 { get; }

        public string PreparedRevisionJson { get; }

        public UserScriptDatabaseRecord(ScriptKey scriptKey, string sourceOrigin, bool isEnabled,
            DateTimeOffset installedAt, DateTimeOffset updatedAt, string sourceSha256, string preparedRevisionJson)
        {
            ScriptKey = scriptKey;
            SourceOrigin = sourceOrigin;
            IsEnabled = isEnabled;
            InstalledAt = installedAt;
            UpdatedAt = updatedAt;
            SourceSha256 = sourceSha256;
            PreparedRevisionJson = preparedRevisionJson;
        }
    }
}
