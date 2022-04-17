namespace CefFlashBrowser.Models
{
    public class ProxySettings
    {
        public bool EnableProxy { get; set; } = false;
        public string IP { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
