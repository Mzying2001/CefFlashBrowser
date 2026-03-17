namespace CefFlashBrowser.Models
{
    public class UserAgentPreset
    {
        public string Name { get; set; }
        public string UserAgent { get; set; }

        public UserAgentPreset(string name, string userAgent)
        {
            Name = name;
            UserAgent = userAgent;
        }
    }
}
