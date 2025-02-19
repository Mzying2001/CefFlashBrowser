using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CefFlashBrowser.Models.Data
{
    public static class GlobalData
    {
        public static string AppBaseDirectory { get; }
        public static string AssetsPath { get; }
        public static string CachesPath { get; }
        public static string CefDllPath { get; }
        public static string PluginsPath { get; }
        public static string FlashPath { get; }
        public static string EmptyExePath { get; }
        public static string SwfPlayerPath { get; }
        public static string SubprocessPath { get; }

        public static string UserDocumentPath { get; }
        public static string DataPath { get; }
        public static string FavoritesPath { get; }
        public static string SettingsPath { get; }

        /// <summary>
        /// true if the program is started with parameters
        /// </summary>
        public static bool IsStartWithoutMainWindow { get; set; } = false;

        static GlobalData()
        {
            AppBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            AssetsPath = Path.Combine(AppBaseDirectory, @"Assets\");
            CachesPath = Path.Combine(AppBaseDirectory, @"Caches\");
            CefDllPath = Path.Combine(AssetsPath, @"CefSharp\");
            PluginsPath = Path.Combine(AssetsPath, @"Plugins\");
            FlashPath = Path.Combine(PluginsPath, @"pepflashplayer.dll");
            EmptyExePath = Path.Combine(AssetsPath, @"EmptyExe\CefFlashBrowser.EmptyExe.exe");
            SwfPlayerPath = Path.Combine(AssetsPath, @"SwfPlayer\swfplayer.html");
            SubprocessPath = Path.Combine(CefDllPath, @"CefSharp.BrowserSubprocess.exe");

            UserDocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DataPath = Path.Combine(UserDocumentPath, @"CefFlashBrowser\");
            FavoritesPath = Path.Combine(DataPath, @"favorites.json");
            SettingsPath = Path.Combine(DataPath, @"settings.json");

            InitData();
        }

        public static void InitData()
        {
            if (!Directory.Exists(DataPath))
            {
                Directory.CreateDirectory(DataPath);
            }

            InitFavorites();
            InitSettings();
        }

        public static void SaveData()
        {
            SaveFavorites();
            SaveSettings();
        }




        #region Favorites

        public static ObservableCollection<Website> Favorites { get; private set; }

        public static void InitFavorites()
        {
            try
            {
                var file = JsonConvert.DeserializeObject<FavoritesFile>(File.ReadAllText(FavoritesPath));
                Favorites = new ObservableCollection<Website>(file.Favorites);
            }
            catch
            {
                Favorites = new ObservableCollection<Website>();
            }
        }

        public static bool SaveFavorites()
        {
            try
            {
                var file = new FavoritesFile { Favorites = Favorites.ToArray() };
                File.WriteAllText(FavoritesPath, JsonConvert.SerializeObject(file, Formatting.Indented));
                return true;
            }
            catch
            { }
            return false;
        }

        #endregion



        #region Settings

        public static Settings Settings { get; private set; }

        public static void InitSettings()
        {
            try
            {
                var file = File.ReadAllText(SettingsPath);
                Settings = JsonConvert.DeserializeObject<Settings>(file);
                Settings.SetNullPropertiesToDefault();
            }
            catch
            {
                Settings = Settings.Default;
            }
        }

        public static bool SaveSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    JObject settingsJson = JObject.Parse(File.ReadAllText(SettingsPath));
                    settingsJson.Merge(JToken.FromObject(Settings));
                    File.WriteAllText(SettingsPath, settingsJson.ToString(Formatting.Indented));
                }
                else
                {
                    File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(Settings, Formatting.Indented));
                }
                return true;
            }
            catch { }
            return false;
        }

        #endregion



    }
}
