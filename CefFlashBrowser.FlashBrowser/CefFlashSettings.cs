namespace CefFlashBrowser.FlashBrowser
{
    public class CefFlashSettings : WinformCefSharp4WPF.CefSettings
    {
        private const string PPAPI_FLASH_PATH = "ppapi-flash-path";
        private const string PPAPI_FLASH_VERSION = "ppapi-flash-version";
        private const string ENABLE_SYSTEM_FLASH = "enable-system-flash";

        public bool EnableSystemFlash
        {
            get => CefCommandLineArgs.ContainsKey(ENABLE_SYSTEM_FLASH) && CefCommandLineArgs[ENABLE_SYSTEM_FLASH] == "1";
            set => CefCommandLineArgs[ENABLE_SYSTEM_FLASH] = value ? "1" : "0";
        }

        public string PpapiFlashVersion
        {
            get => CefCommandLineArgs.ContainsKey(PPAPI_FLASH_VERSION) ? CefCommandLineArgs[PPAPI_FLASH_VERSION] : null;
            set => CefCommandLineArgs[PPAPI_FLASH_VERSION] = value;
        }

        public string PpapiFlashPath
        {
            get => CefCommandLineArgs.ContainsKey(PPAPI_FLASH_PATH) ? CefCommandLineArgs[PPAPI_FLASH_PATH] : null;
            set => CefCommandLineArgs[PPAPI_FLASH_PATH] = value;
        }
    }
}
