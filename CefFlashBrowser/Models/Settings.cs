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
        /// The address to open when creating a new tab
        /// </summary>
        public string NewTabAddress { get; set; }

        /// <summary>
        /// Determines whether the "onbeforeunload" dialog is disabled
        /// </summary>
        public bool DisableOnBeforeUnloadDialog { get; set; }

        /// <summary>
        /// Determines whether SWFs opened with their source page context can access scripts in the player page
        /// </summary>
        public bool AllowPageSwfScriptAccess { get; set; }

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
        /// The size and position of the userscript manager window
        /// </summary>
        public WindowSizeInfo UserScriptManagerSizeInfo { get; set; }

        /// <summary>
        /// Display and indentation preferences for the userscript editor
        /// </summary>
        public EditorSettings ScriptEditorSettings { get; set; }

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
        /// Determines whether the tab strip remains visible with a single tab
        /// </summary>
        public bool AlwaysShowTabStrip { get; set; }

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
        /// Use software rendering for WPF if set to true
        /// </summary>
        public bool DisableGpuAccelerationWPF { get; set; }

        /// <summary>
        /// The emoticon on main window title, or null for random one
        /// </summary>
        public string CustomEmoticon { get; set; }

        /// <summary>
        /// The browser context menu will be disabled if set to true
        /// </summary>
        public bool DisableBrowserContextMenu { get; set; }

        /// <summary>
        /// Determines whether core-js polyfills are injected at document start
        /// </summary>
        public bool EnableCoreJsPolyfills { get; set; }

        /// <summary>
        /// Determines whether userscript execution is enabled
        /// </summary>
        public bool EnableUserScripts { get; set; }

        /// <summary>
        /// Determines whether userscripts can use privileged APIs in the page execution world
        /// </summary>
        public bool TrustedUserScriptPageWorld { get; set; }

        /// <summary>
        /// The default behavior for userscript permissions without a remembered decision.
        /// </summary>
        public UserScriptPermissionBehavior DefaultUserScriptPermissionBehavior { get; set; }

        /// <summary>
        /// The directory for userscript downloads; defaults to the system Downloads folder when blank
        /// </summary>
        public string UserScriptDownloadDirectory { get; set; }

        /// <summary>
        /// The remembered download directory, or null for the system Downloads folder.
        /// </summary>
        public string DownloadDirectory { get; set; }


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
            NewPageBehavior = NewPageBehavior.NewTab;
            NewTabAddress = "about:blank";
            DisableOnBeforeUnloadDialog = false;
            AllowPageSwfScriptAccess = true;
            MainWindowSizeInfo = null;
            BrowserWindowSizeInfo = null;
            SwfWindowSizeInfo = null;
            SolSaveManagerSizeInfo = null;
            UserScriptManagerSizeInfo = null;
            ScriptEditorSettings = new EditorSettings();
            ProxySettings = new ProxySettings();
            UserAgentSetting = new UserAgentSetting();
            FakeFlashVersionSetting = new FakeFlashVersionSetting();
            HideMainWindowOnBrowsing = false;
            FollowSystemTheme = true;
            EnableIntegratedDevTools = true;
            IntegratedDevToolsWidth = 300d;
            SaveZoomLevel = true;
            AlwaysShowTabStrip = false;
            BrowserZoomLevel = 0d;
            DisableFullscreen = false;
            RetainedLogCount = 30;
            DisableBrowserShortcuts = false;
            DisableGpuAcceleration = false;
            CustomEmoticon = null;
            DisableBrowserContextMenu = false;
            EnableCoreJsPolyfills = true;
            EnableUserScripts = true;
            TrustedUserScriptPageWorld = true;
            DefaultUserScriptPermissionBehavior = UserScriptPermissionBehavior.Prompt;
            UserScriptDownloadDirectory = null;
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
