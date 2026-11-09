using CefFlashBrowser.Log;
using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;

namespace CefFlashBrowser.Services
{
    public class FavoritesService
    {
        private readonly string _favoritesPath;
        private readonly ILogger _logger;

        public ObservableCollection<Website> Favorites { get; } = new ObservableCollection<Website>();

        public FavoritesService(string favoritesPath, ILogger logger)
        {
            _favoritesPath = favoritesPath ?? throw new ArgumentNullException(nameof(favoritesPath));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Load();
        }

        private void Load()
        {
            if (!File.Exists(_favoritesPath))
            {
                _logger.Log(LogLevel.Info, "Favorites file does not exist, starting with empty favorites");
                return;
            }

            try
            {
                var json = File.ReadAllText(_favoritesPath, Encoding.UTF8);
                var file = JsonConvert.DeserializeObject<FavoritesFile>(json);

                if (file?.Favorites == null)
                {
                    _logger.Log(LogLevel.Error, $"Favorites file is malformed (no Favorites array): {_favoritesPath}");
                    return;
                }

                foreach (var website in file.Favorites)
                    Favorites.Add(website);
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error,
                    $"Failed to load favorites file, starting with empty favorites: {_favoritesPath}", e);
            }
        }

        public bool Save()
        {
            try
            {
                Website[] snapshot;

                lock (Favorites)
                {
                    snapshot = Favorites.ToArray();
                }

                var file = new FavoritesFile { Favorites = snapshot };
                var json = JsonConvert.SerializeObject(file, Formatting.Indented);
                FileHelper.SafeWriteFile(_favoritesPath, json, Encoding.UTF8);
                _logger.Log(LogLevel.Info, "Favorites saved successfully");
                return true;
            }
            catch (Exception e)
            {
                _logger.Log(LogLevel.Error, "Failed to save favorites", e);
                return false;
            }
        }
    }
}
