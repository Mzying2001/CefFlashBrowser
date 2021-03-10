using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    interface IFavoritesDataService
    {
        string FavoritesDataFileName { get; set; }
        List<Website> GetFavorites();
        void WriteFavorites(IEnumerable<Website> websites);
    }
}
