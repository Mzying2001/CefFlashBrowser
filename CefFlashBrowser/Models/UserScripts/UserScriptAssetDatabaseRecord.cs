namespace CefFlashBrowser.Models.UserScripts
{
    public sealed class UserScriptAssetDatabaseRecord
    {
        public string ScriptId { get; }
        public int Kind { get; }
        public int Ordinal { get; }
        public string Name { get; }
        public string Url { get; }
        public string ContentSha256 { get; }
        public string Mime { get; }
        public string Charset { get; }

        public UserScriptAssetDatabaseRecord(string scriptId, int kind, int ordinal, string name,
            string url, string contentSha256, string mime, string charset)
        {
            ScriptId = scriptId;
            Kind = kind;
            Ordinal = ordinal;
            Name = name;
            Url = url;
            ContentSha256 = contentSha256;
            Mime = mime;
            Charset = charset;
        }
    }
}
