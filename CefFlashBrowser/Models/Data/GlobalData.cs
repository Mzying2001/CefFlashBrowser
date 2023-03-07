using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CefFlashBrowser.Models.Data
{
    public static class GlobalData
    {
        public static string AppBaseDirectory { get; }
        public static string EmptyExePath { get; }
        public static string AssetsPath { get; }
        public static string CachesPath { get; }
        public static string PluginsPath { get; }
        public static string FlashPath { get; }

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
            EmptyExePath = Path.Combine(AppBaseDirectory, @"CefFlashBrowser.EmptyExe.exe");
            AssetsPath = Path.Combine(AppBaseDirectory, @"Assets\");
            CachesPath = Path.Combine(AppBaseDirectory, @"Caches\");
            PluginsPath = Path.Combine(AssetsPath, @"Plugins\");
            FlashPath = Path.Combine(PluginsPath, @"pepflashplayer.dll");

            UserDocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DataPath = Path.Combine(UserDocumentPath, @"CefFlashBrowser\");
            FavoritesPath = Path.Combine(DataPath, @"Favorites.json");
            SettingsPath = Path.Combine(DataPath, @"Settings.json");

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

        public static void SaveFavorites()
        {
            var file = new FavoritesFile { Favorites = Favorites.ToArray() };
            File.WriteAllText(FavoritesPath, JsonConvert.SerializeObject(file, Formatting.Indented));
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
            }
            catch
            {
                Settings = Settings.Default;
            }
        }

        public static void SaveSettings()
        {
            File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(Settings, Formatting.Indented));
        }

        #endregion



    }
}
