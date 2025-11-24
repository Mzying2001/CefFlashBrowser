namespace CefFlashBrowser.Models
{
    public class Settings
    {
        /// <summary>
        /// True if the application is started for the first time
        /// </summary>
        public bool FirstStart { get; set; }

        /// <summary>
        /// Current UI language (e.g. "zh-CN", "en-US")
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Current theme (Light or Dark)
        /// </summary>
        public Theme Theme { get; set; }

        /// <summary>
        /// Search engine to use for searches from the address bar
        /// </summary>
        public SearchEngine SearchEngine { get; set; }

        /// <summary>
        /// Navigation type in main window
        /// </summary>
        public NavigationType NavigationType { get; set; }

        /// <summary>
        /// Determines how new pages are opened in the browser
        /// </summary>
        public NewPageBehavior NewPageBehavior { get; set; }

        /// <summary>
        /// Determines whether the "onbeforeunload" dialog is disabled
        /// </summary>
        public bool DisableOnBeforeUnloadDialog { get; set; }

        /// <summary>
        /// The size and position of the main window
        /// </summary>
        public WindowSizeInfo MainWindowSizeInfo { get; set; }

        /// <summary>
        /// The size and position of the browser window
        /// </summary>
        public WindowSizeInfo BrowserWindowSizeInfo { get; set; }

        /// <summary>
        /// The size and position of the SWF player window
        /// </summary>
        public WindowSizeInfo SwfWindowSizeInfo { get; set; }

        /// <summary>
        /// The size and position of the SOL save manager window
        /// </summary>
        public WindowSizeInfo SolSaveManagerSizeInfo { get; set; }

        /// <summary>
        /// Proxy settings for the browser
        /// </summary>
        public ProxySettings ProxySettings { get; set; }

        /// <summary>
        /// User agent settings for the browser
        /// </summary>
        public UserAgentSetting UserAgentSetting { get; set; }

        /// <summary>
        /// The fake Flash version settings
        /// </summary>
        public FakeFlashVersionSetting FakeFlashVersionSetting { get; set; }

        /// <summary>
        /// Determines whether the main window is hidden when navigating to a new page
        /// </summary>
        public bool HideMainWindowOnBrowsing { get; set; }

        /// <summary>
        /// Determines whether the application theme follows the system theme
        /// </summary>
        public bool FollowSystemTheme { get; set; }

        /// <summary>
        /// Determines whether the integrated developer tools are enabled
        /// </summary>
        public bool EnableIntegratedDevTools { get; set; }

        /// <summary>
        /// The width of the integrated developer tools panel
        /// </summary>
        public double IntegratedDevToolsWidth { get; set; }

        /// <summary>
        /// Determines whether the browser zoom level should be saved
        /// </summary>
        public bool SaveZoomLevel { get; set; }

        /// <summary>
        /// The browser zoom level
        /// </summary>
        public double BrowserZoomLevel { get; set; }

        /// <summary>
        /// The browser window will not enter full screen mode if this is set to true
        /// </summary>
        public bool DisableFullscreen { get; set; }

        /// <summary>
        /// The number of log files to retain
        /// </summary>
        public int RetainedLogCount { get; set; }

        /// <summary>
        /// Disables shortcuts in the browser window if set to true
        /// </summary>
        public bool DisableBrowserShortcuts { get; set; }

        /// <summary>
        /// Disable GPU acceleration if set to true
        /// </summary>
        public bool DisableGpuAcceleration { get; set; }

        /// <summary>
        /// The emoticon on main window title, or null for random one
        /// </summary>
        public string CustomEmoticon { get; set; }


        /// <summary>
        /// Default settings
        /// </summary>
        public static Settings Default => new Settings();


        /// <summary>
        /// Constructor that initializes default settings
        /// </summary>
        public Settings()
        {
            // default settings
            FirstStart = true;
            Language = "zh-CN";
            Theme = Theme.Light;
            SearchEngine = SearchEngine.Bing;
            NavigationType = NavigationType.Automatic;
            NewPageBehavior = NewPageBehavior.OriginalWindow;
            DisableOnBeforeUnloadDialog = false;
            MainWindowSizeInfo = null;
            BrowserWindowSizeInfo = null;
            SwfWindowSizeInfo = null;
            SolSaveManagerSizeInfo = null;
            ProxySettings = new ProxySettings();
            UserAgentSetting = new UserAgentSetting();
            FakeFlashVersionSetting = new FakeFlashVersionSetting();
            HideMainWindowOnBrowsing = false;
            FollowSystemTheme = true;
            EnableIntegratedDevTools = true;
            IntegratedDevToolsWidth = 300d;
            SaveZoomLevel = true;
            BrowserZoomLevel = 0d;
            DisableFullscreen = false;
            RetainedLogCount = 30;
            DisableBrowserShortcuts = false;
            DisableGpuAcceleration = false;
            CustomEmoticon = null;
        }

        /// <summary>
        /// Sets any null properties to their default values
        /// </summary>
        public void SetNullPropertiesToDefault()
        {
            var defaultSettings = Default;

            foreach (var property in GetType().GetProperties())
            {
                if (property.GetValue(this) == null)
                    property.SetValue(this, property.GetValue(defaultSettings));
            }
        }
    }
}
