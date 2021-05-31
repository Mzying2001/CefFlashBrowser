using CefFlashBrowser.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Models.StaticData
{
    static class Favorites
    {
        public static ObservableCollection<Website> Items { get; private set; }

        public static void InitFavorites()
        {
            Items = new ObservableCollection<Website>();
            foreach (var website in new FavoritesDataService().GetFavorites())
                Items.Add(website);
        }

        public static void SaveFavorites()
        {
            new FavoritesDataService().WriteFavorites(Items);
        }
    }
}
