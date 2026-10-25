namespace CefFlashBrowser.Models
{
    /// <summary>
    /// Specifies how navigation input is handled.
    /// </summary>
    public enum NavigationType
    {
        Automatic,
        SearchOnly,
        NavigateOnly,
    }

    /// <summary>
    /// Specifies where a new page is opened.
    /// </summary>
    public enum NewPageBehavior
    {
        OriginalWindow,
        NewWindow,
        NewTab,
    }

    /// <summary>
    /// Identifies a supported search engine.
    /// </summary>
    public enum SearchEngine
    {
        Baidu,
        Google,
        Bing,
        Sogou,
        So360,
        DuckDuckGo,
        Yandex,
        Bilibili,
        Game4399,
    }

    /// <summary>
    /// Specifies the application's visual theme.
    /// </summary>
    public enum Theme
    {
        Light,
        Dark,
    }

    /// <summary>
    /// Stores settings for overriding the reported Flash Player version.
    /// </summary>
    public class FakeFlashVersionSetting
    {
        public bool Enable { get; set; } = true;
        public string FlashVersion { get; set; } = "99.0.0.999";
    }

    /// <summary>
    /// Stores proxy server and authentication settings.
    /// </summary>
    public class ProxySettings
    {
        public bool EnableProxy { get; set; } = false;
        public string IP { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a named user agent preset.
    /// </summary>
    public class UserAgentPreset
    {
        public string Name { get; set; }
        public string UserAgent { get; set; }

        public UserAgentPreset()
        {
            Name = string.Empty;
            UserAgent = string.Empty;
        }

        public UserAgentPreset(string name, string userAgent)
        {
            Name = name;
            UserAgent = userAgent;
        }
    }

    /// <summary>
    /// Stores the custom user agent configuration.
    /// </summary>
    public class UserAgentSetting
    {
        public bool EnableCustom { get; set; } = false;
        public string UserAgent { get; set; } = string.Empty;
    }
}
