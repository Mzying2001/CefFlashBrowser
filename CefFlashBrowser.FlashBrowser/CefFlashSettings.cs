namespace CefFlashBrowser.FlashBrowser
{
    public class CefFlashSettings : WinformCefSharp4WPF.CefSettings
    {
        private const string PPAPI_FLASH_PATH = "ppapi-flash-path";
        private const string PPAPI_FLASH_VERSION = "ppapi-flash-version";
        private const string ENABLE_SYSTEM_FLASH = "enable-system-flash";

        public bool EnableSystemFlash
        {
            get => CefCommandLineArgs[ENABLE_SYSTEM_FLASH] == "1";
            set => CefCommandLineArgs[ENABLE_SYSTEM_FLASH] = value ? "1" : "0";
        }

        public string PpapiFlashVersion
        {
            get => CefCommandLineArgs[PPAPI_FLASH_VERSION];
            set => CefCommandLineArgs[PPAPI_FLASH_VERSION] = value;
        }

        public string PpapiFlashPath
        {
            get => CefCommandLineArgs[PPAPI_FLASH_PATH];
            set => CefCommandLineArgs[PPAPI_FLASH_PATH] = value;
        }

        public CefFlashSettings()
        {
            PpapiFlashPath = default;
            PpapiFlashVersion = default;
            EnableSystemFlash = default;
        }
    }
}
