using CefFlashBrowser.Utils;
using Newtonsoft.Json;

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
        NewTab,
        NewWindow,
        OriginalWindow,
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
    /// Specifies how to handle userscript permission requests without a remembered decision.
    /// </summary>
    public enum UserScriptPermissionBehavior
    {
        Prompt,
        Allow,
        Deny,
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
    /// Stores display and indentation preferences for a text editor.
    /// </summary>
    public sealed class EditorSettings
    {
        internal const string DefaultFontFamily = "Consolas";
        internal const int DefaultFontSize = 14;
        internal const int DefaultTabWidth = 2;

        private int _fontSize = DefaultFontSize;
        private int _tabWidth = DefaultTabWidth;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string FontFamily { get; set; } = DefaultFontFamily;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int FontSize
        {
            get => _fontSize;
            set => _fontSize = IsFontSizeValid(value) ? value : DefaultFontSize;
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool IndentWithSpaces { get; set; } = true;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int TabWidth
        {
            get => _tabWidth;
            set => _tabWidth = IsTabWidthValid(value) ? value : DefaultTabWidth;
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool ShowLineNumbers { get; set; } = true;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool WordWrap { get; set; }

        internal static bool IsFontSizeValid(int value) => value >= 8 && value <= 72;

        internal static bool IsTabWidthValid(int value) => value >= 1 && value <= 16;

        internal string GetIndentationString() => IndentWithSpaces ? new string(' ', TabWidth) : "\t";
    }

    /// <summary>
    /// Stores proxy server and authentication settings.
    /// </summary>
    public class ProxySettings
    {
        private string _password = string.Empty;

        public bool EnableProxy { get; set; } = false;
        public string IP { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// The stored DPAPI ciphertext. Legacy plaintext and unreadable values are discarded.
        /// </summary>
        public string Password
        {
            get => _password;
            set => _password = string.IsNullOrEmpty(PasswordProtector.UnprotectOrEmpty(value)) ? string.Empty : value;
        }

        /// <summary>
        /// The password used by the settings UI and proxy authentication, or empty if unavailable.
        /// </summary>
        [JsonIgnore]
        public string PlainPassword
        {
            get => PasswordProtector.UnprotectOrEmpty(_password);
            set => _password = PasswordProtector.Protect(value);
        }
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
