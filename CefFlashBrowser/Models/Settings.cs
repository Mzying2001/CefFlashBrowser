namespace CefFlashBrowser.Models
{
    public class Settings
    {
        public string Language { get; set; }
        public SearchEngine.Engine SearchEngine { get; set; }
        public int AddressBarFunction { get; set; }
        public bool FirstStart { get; set; }
        public WindowSizeInfo MainWindowSizeInfo { get; set; }
        public WindowSizeInfo BrowserWindowSizeInfo { get; set; }
        public WindowSizeInfo SwfWindowSizeInfo { get; set; }



        public static Settings Default => new Settings
        {
            Language = "zh-CN",
            SearchEngine = Models.SearchEngine.Engine.Baidu,
            AddressBarFunction = 0,
            FirstStart = true,
            MainWindowSizeInfo = null,
            BrowserWindowSizeInfo = null,
            SwfWindowSizeInfo = null
        };
    }
}
