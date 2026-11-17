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

        public string CommitId { get; }

        public UserScriptDatabaseRecord(ScriptKey scriptKey, string sourceOrigin, bool isEnabled,
            DateTimeOffset installedAt, DateTimeOffset updatedAt, string sourceSha256, string commitId)
        {
            ScriptKey = scriptKey;
            SourceOrigin = sourceOrigin;
            IsEnabled = isEnabled;
            InstalledAt = installedAt;
            UpdatedAt = updatedAt;
            SourceSha256 = sourceSha256;
            CommitId = commitId;
        }
    }
}
