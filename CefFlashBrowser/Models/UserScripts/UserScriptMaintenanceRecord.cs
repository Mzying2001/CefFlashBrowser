using Mzying2001.MonkeySharp.Core.Repository;
using System.Collections.Generic;

namespace CefFlashBrowser.Models.UserScripts
{
    public sealed class UserScriptMaintenanceRecord
    {
        public UserScriptPersistenceRecord Record { get; }
        public IReadOnlyList<string> Diagnostics { get; }

        public UserScriptMaintenanceRecord(UserScriptPersistenceRecord record, IReadOnlyList<string> diagnostics)
        {
            Record = record;
            Diagnostics = diagnostics;
        }
    }
}
