using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.IO;
using Newtonsoft.Json;
using CefFlashBrowser.Models;

namespace CefFlashBrowser.Services
{
    class JsonDataService : IFavoritesDataService
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

        public static JObject ReadFile(string fileName)
        {
            var json = new JObject();

            if (!File.Exists(fileName))
                return json;

            using (var reader = File.OpenText(fileName))
            using (var jsreader = new JsonTextReader(reader))
                json = (JObject)JToken.ReadFrom(jsreader);

            return json;
        }

        public static void WriteFile(string fileName, JObject json)
        {
            string dir = fileName.Substring(0, fileName.LastIndexOf('\\'));
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(fileName, json.ToString());
        }
    }
}
