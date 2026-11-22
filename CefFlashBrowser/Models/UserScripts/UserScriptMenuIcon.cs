namespace CefFlashBrowser.Models.UserScripts
{
    internal sealed class UserScriptMenuIcon
    {
        public string Url { get; }

        public string Initial { get; }

        public UserScriptMenuIcon(string url, string initial)
        {
            Url = url;
            Initial = initial;
        }
    }
}
