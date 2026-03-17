namespace CefFlashBrowser.Data
{
    /// <summary>
    /// MVVM Messenger tokens for cross-component communication.
    /// </summary>
    public static class MessageTokens
    {
        /// <summary>Sent when the theme changes, notifying windows to update title bar colors.</summary>
        public const string THEME_CHANGED = "THEME_CHANGED";

        /// <summary>Sent when the language changes, notifying UI to refresh localized text.</summary>
        public const string LANGUAGE_CHANGED = "LANGUAGE_CHANGED";

        /// <summary>Hides the main window when browsing (when HideMainWindowOnBrowsing is enabled).</summary>
        public const string CLOSE_MAINWINDOW = "CLOSE_MAINWINDOW";

        /// <summary>Sent when the settings window closes, triggering settings persistence to file.</summary>
        public const string SAVE_SETTINGS = "SAVE_SETTINGS";

        /// <summary>Sent when the favorites window closes, triggering favorites persistence to file.</summary>
        public const string SAVE_FAVORITES = "SAVE_FAVORITES";

        /// <summary>Sent when DevTools opens, showing the embedded DevTools panel.</summary>
        public const string DEVTOOLS_OPENED = "DEVTOOLS_OPENED";

        /// <summary>Sent when DevTools closes, hiding the embedded DevTools panel.</summary>
        public const string DEVTOOLS_CLOSED = "DEVTOOLS_CLOSED";

        /// <summary>Sent when fullscreen state toggles, adjusting window borders and maximization.</summary>
        public const string FULLSCREEN_CHANGED = "FULLSCREEN_CHANGED";

        /// <summary>Sent when clearing cache or switching architecture, force-closing all browser instances.</summary>
        public const string CLOSE_ALL_BROWSERS = "CLOSE_ALL_BROWSERS";

        /// <summary>Sent when theme settings change, redrawing all window title bars.</summary>
        public const string REDRAW_ALL_FRAMES = "REDRAW_ALL_FRAMES";

        /// <summary>Sent when Ctrl+F is pressed while the find popup is already open, refocusing the search box.</summary>
        public const string FOCUS_FIND_POPUP = "FOCUS_FIND_POPUP";
    }
}
