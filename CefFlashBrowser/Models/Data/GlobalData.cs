using CefFlashBrowser.Views.Dialogs.JsDialogs;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace CefFlashBrowser.Models.Data
{
    public static class GlobalData
    {
        public static string DocumentPath => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public static string DataPath { get; }
        public static string FavoritesPath { get; }
        public static string SettingsPath { get; }

        static GlobalData()
        {
            DataPath = Path.Combine(DocumentPath, "CefFlashBrowser");
            FavoritesPath = Path.Combine(DataPath, "Favorites.json");
            SettingsPath = Path.Combine(DataPath, "Settings.json");
        }

        public static void InitData()
        {
            if (!Directory.Exists(DataPath))
                Directory.CreateDirectory(DataPath);

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
            Favorites = new ObservableCollection<Website>();
            try
            {
                var file = JsonConvert.DeserializeObject<FavoritesFile>(File.ReadAllText(FavoritesPath));
                foreach (var item in file.Favorites)
                    Favorites.Add(item);
            }
            catch (Exception e)
            {
                JsAlertDialog.Show(e.Message, LanguageManager.GetString("title_error"));
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
