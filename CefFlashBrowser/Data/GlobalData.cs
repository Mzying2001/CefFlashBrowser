using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleMvvm.Messaging;
using System;
using System.IO;
using System.Text;

namespace CefFlashBrowser.Data
{
    public static class GlobalData
    {
        public static string AppBaseDirectory { get; }
        public static string AssetsPath { get; }
        public static string CachesPath { get; }
        public static string LogsPath { get; }
        public static string CefDllPath { get; }
        public static string SqliteDllPath { get; }
        public static string PluginsPath { get; }
        public static string SharedObjectsPath { get; }

        public static string CefLogPath { get; }
        public static string FlashPath { get; }
        public static string EmptyExePath { get; }
        public static string SwfPlayerPath { get; }
        public static string SubprocessPath { get; }

        public static string DataPath { get; }
        public static string FavoritesPath { get; }
        public static string SettingsPath { get; }
        public static string DatabasePath { get; }

        /// <summary>
        /// true if the program is started with parameters
        /// </summary>
        public static bool IsStartWithoutMainWindow { get; set; } = false;

        static GlobalData()
        {
            AppBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            AssetsPath = Path.Combine(AppBaseDirectory, "Assets\\");
            CachesPath = Path.Combine(AppBaseDirectory, "Caches\\");
            LogsPath = Path.Combine(AppBaseDirectory, "Logs\\");
            CefDllPath = Path.Combine(AssetsPath, "CefSharp\\");
            SqliteDllPath = Path.Combine(AssetsPath, "SQLite\\");
            PluginsPath = Path.Combine(AssetsPath, "Plugins\\");
            SharedObjectsPath = Path.Combine(CachesPath, "Pepper Data\\Shockwave Flash\\WritableRoot\\#SharedObjects\\");

            CefLogPath = Path.Combine(LogsPath, $"cef_{DateTime.Now:yyyyMMdd}.log");
            FlashPath = Path.Combine(PluginsPath, "pepflashplayer.dll");
            EmptyExePath = Path.Combine(AssetsPath, "EmptyExe\\CefFlashBrowser.EmptyExe.exe");
            SwfPlayerPath = Path.Combine(AssetsPath, "SwfPlayer\\swfplayer.html");
            SubprocessPath = Path.Combine(CefDllPath, "CefSharp.BrowserSubprocess.exe");

            var userDocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Older versions stored the CefFlashBrowser folder in the user's Documents directory.
            // Since v1.1.8, it has moved to LocalAppData. GetRedirectDirectory returns the old
            // directory if it exists; otherwise, it returns the new directory.
            DataPath = DirectoryHelper.GetRedirectDirectory(userDocumentPath, "CefFlashBrowser\\", localAppDataPath);

            FavoritesPath = Path.Combine(DataPath, "favorites.json");
            SettingsPath = Path.Combine(DataPath, "settings.json");
            DatabasePath = Path.Combine(DataPath, "browser.db");

            Messenger.Global.Register(MessageTokens.SAVE_SETTINGS, _ => SaveSettings());
        }

        public static string GetAppLogPath(DateTime time)
        {
            return Path.Combine(LogsPath, $"app_{time:yyyyMMdd}.log");
        }

        public static bool InitData()
        {
            DirectoryHelper.EnsureDirectoryExists(DataPath);
            DirectoryHelper.EnsureDirectoryExists(LogsPath);
            DirectoryHelper.EnsureDirectoryExists(CachesPath);

            InitSettings();
            return true;
        }

        public static void SaveData()
        {
            SaveSettings();
        }

        #region Settings

        public static Settings Settings { get; private set; }

        public static void InitSettings()
        {
            try
            {
                var json = File.ReadAllText(SettingsPath, Encoding.UTF8);
                Settings = JsonConvert.DeserializeObject<Settings>(json);
                Settings.SetNullPropertiesToDefault();
            }
            catch (Exception e)
            {
                Settings = Settings.Default;
                LogHelper.LogError("Settings file not found or invalid, using default settings", e);
            }
        }

        public static bool SaveSettings()
        {
            try
            {
                bool fileExists = File.Exists(SettingsPath);

                if (fileExists)
                {
                    string oldSettingsContent = File.ReadAllText(SettingsPath, Encoding.UTF8);

                    try
                    {
                        // Merge with existing settings
                        JObject settingsJson = JObject.Parse(oldSettingsContent);
                        settingsJson.Merge(JToken.FromObject(Settings));

                        var jsonString = settingsJson.ToString(Formatting.Indented);
                        FileHelper.SafeWriteFile(SettingsPath, jsonString, Encoding.UTF8);

                        LogHelper.LogInfo("Settings saved successfully, type: merge");
                        return true;
                    }
                    catch (JsonReaderException e)
                    {
                        LogHelper.LogError("Settings merge failed, falling back to overwrite", e);
                        LogHelper.LogInfo("Old settings content: " + oldSettingsContent);
                    }
                }

                // File does not exist or merge failed
                var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
                FileHelper.SafeWriteFile(SettingsPath, json, Encoding.UTF8);

                LogHelper.LogInfo("Settings saved successfully, type: " + (fileExists ? "overwrite" : "create"));
                return true;
            }
            catch (Exception e)
            {
                LogHelper.LogError("Failed to save settings", e);
                return false;
            }
        }

        #endregion

    }
}
