using CefFlashBrowser.Models;
using CefFlashBrowser.Services.Interfaces;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    class FavoritesDataService : JsonDataService, IFavoritesDataService
    {
        public string FavoritesDataFileName { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), @"CefFlashBrowser\favorites.json");

        public List<Website> GetFavorites()
        {
            var json = ReadFile(FavoritesDataFileName);
            var list = new List<Website>();

            var favorties = json["Favorites"];
            if (favorties == null)
                return list;

            foreach (var item in favorties)
                list.Add(new Website(item["Name"].ToString(), item["Url"].ToString()));

            return list;
        }

        public void WriteFavorites(IEnumerable<Website> websites)
        {
            var favorites = new JArray();
            foreach (var item in websites)
            {
                favorites.Add(new JObject
                {
                    ["Name"] = item.Name,
                    ["Url"] = item.Url
                });
            }
            var json = new JObject
            {
                ["Favorites"] = favorites
            };
            WriteFile(FavoritesDataFileName, json);
        }
    }
}
