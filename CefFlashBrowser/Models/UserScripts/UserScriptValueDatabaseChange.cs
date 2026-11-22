using Mzying2001.MonkeySharp.Core.Storage;

namespace CefFlashBrowser.Models.UserScripts
{
    public sealed class UserScriptValueDatabaseChange
    {
        public string Key { get; }
        public StoredValue OldValue { get; }
        public StoredValue NewValue { get; }

        public UserScriptValueDatabaseChange(string key, StoredValue oldValue, StoredValue newValue)
        {
            Key = key;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
}
