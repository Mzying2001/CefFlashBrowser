namespace CefFlashBrowser.Models
{
    public class Settings
    {
        public bool FirstStart { get; set; }
        public string Language { get; set; }
        public SearchEngine SearchEngine { get; set; }
        public NavigationType NavigationType { get; set; }
        public NewPageBehavior NewPageBehavior { get; set; }
        public bool DisableOnBeforeUnloadDialog { get; set; }
        public WindowSizeInfo BrowserWindowSizeInfo { get; set; }
        public WindowSizeInfo SwfWindowSizeInfo { get; set; }
        public ProxySettings ProxySettings { get; set; }
        public UserAgentSetting UserAgentSetting { get; set; }
        public FakeFlashVersionSetting FakeFlashVersionSetting { get; set; }
        public bool HideMainWindowOnBrowsing { get; set; }


        public static Settings Default => new Settings();


        public Settings()
        {
            // default settings
            FirstStart = true;
            Language = "zh-CN";
            SearchEngine = SearchEngine.Baidu;
            NavigationType = NavigationType.Automatic;
            NewPageBehavior = NewPageBehavior.OriginalWindow;
            DisableOnBeforeUnloadDialog = false;
            BrowserWindowSizeInfo = null;
            SwfWindowSizeInfo = null;
            ProxySettings = new ProxySettings();
            UserAgentSetting = new UserAgentSetting();
            FakeFlashVersionSetting = new FakeFlashVersionSetting();
            HideMainWindowOnBrowsing = false;
        }
    }
}
