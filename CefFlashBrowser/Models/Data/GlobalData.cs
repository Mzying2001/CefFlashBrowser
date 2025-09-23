﻿using CefFlashBrowser.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimpleMvvm.Messaging;
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
        public static string LogsPath { get; }
        public static string CefDllPath { get; }
        public static string PluginsPath { get; }
        public static string SharedObjectsPath { get; }

        public static string AppLogPath { get; }
        public static string CefLogPath { get; }
        public static string FlashPath { get; }
        public static string EmptyExePath { get; }
        public static string SwfPlayerPath { get; }
        public static string SubprocessPath { get; }

        public static string UserDocumentPath { get; }
        public static string DataPath { get; }
        public static string FavoritesPath { get; }
        public static string SettingsPath { get; }

        /// <summary>
        /// Number of log files to keep
        /// </summary>
        public static int RetainedLogCount { get; } = 30;

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
            PluginsPath = Path.Combine(AssetsPath, "Plugins\\");
            SharedObjectsPath = Path.Combine(CachesPath, "Pepper Data\\Shockwave Flash\\WritableRoot\\#SharedObjects\\");

            AppLogPath = Path.Combine(LogsPath, $"app_{DateTime.Now:yyyyMMdd}.log");
            CefLogPath = Path.Combine(LogsPath, $"cef_{DateTime.Now:yyyyMMdd}.log");
            FlashPath = Path.Combine(PluginsPath, "pepflashplayer.dll");
            EmptyExePath = Path.Combine(AssetsPath, "EmptyExe\\CefFlashBrowser.EmptyExe.exe");
            SwfPlayerPath = Path.Combine(AssetsPath, "SwfPlayer\\swfplayer.html");
            SubprocessPath = Path.Combine(CefDllPath, "CefSharp.BrowserSubprocess.exe");

            UserDocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            DataPath = Path.Combine(UserDocumentPath, "CefFlashBrowser\\");
            FavoritesPath = Path.Combine(DataPath, "favorites.json");
            SettingsPath = Path.Combine(DataPath, "settings.json");

            Messenger.Global.Register(MessageTokens.SAVE_SETTINGS, _ => SaveSettings());
            Messenger.Global.Register(MessageTokens.SAVE_FAVORITES, _ => SaveFavorites());
        }

        private static void CreateDirIfNotExist(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static bool InitData()
        {
            CreateDirIfNotExist(DataPath);
            CreateDirIfNotExist(CachesPath);
            CreateDirIfNotExist(LogsPath);

            InitFavorites();
            InitSettings();
            return true;
        }

        public static void SaveData()
        {
            SaveFavorites();
            SaveSettings();
        }

        private static void SafeWriteFile(string path, string contents)
        {
            var tmpPath = path + ".tmp";
            File.WriteAllText(tmpPath, contents);
            File.Copy(tmpPath, path, true);
            File.Delete(tmpPath);
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
            catch (Exception e)
            {
                Favorites = new ObservableCollection<Website>();
                LogHelper.LogError("Favorites file not found or invalid, using empty favorites", e);
            }
        }

        public static bool SaveFavorites()
        {
            try
            {
                var file = new FavoritesFile { Favorites = Favorites.ToArray() };
                SafeWriteFile(FavoritesPath, JsonConvert.SerializeObject(file, Formatting.Indented));
                LogHelper.LogInfo("Favorites saved successfully");
                return true;
            }
            catch (Exception e)
            {
                LogHelper.LogError("Failed to save favorites", e);
                return false;
            }
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
                if (File.Exists(SettingsPath))
                {
                    JObject settingsJson = JObject.Parse(File.ReadAllText(SettingsPath));
                    settingsJson.Merge(JToken.FromObject(Settings));
                    SafeWriteFile(SettingsPath, settingsJson.ToString(Formatting.Indented));
                    LogHelper.LogInfo("Settings saved successfully, type: merge");
                }
                else
                {
                    SafeWriteFile(SettingsPath, JsonConvert.SerializeObject(Settings, Formatting.Indented));
                    LogHelper.LogInfo("Settings saved successfully, type: create");
                }
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
