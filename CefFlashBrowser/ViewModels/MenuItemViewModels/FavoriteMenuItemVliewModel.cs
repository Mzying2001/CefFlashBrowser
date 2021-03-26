using CefFlashBrowser.Models;
using CefFlashBrowser.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.MenuItemViewModels
{
    class FavoriteMenuItemVliewModel : MenuItemViewModel
    {
        private Website _website;

        public Website Website
        {
            get => _website;
            set
            {
                _website = value;
                Header = value.Name;
            }
        }

        protected override void MenuItemSelected()
        {
            BrowserWindow.Popup(Website.Url);
        }

        public FavoriteMenuItemVliewModel(Website website) : base()
        {
            Header = website.Name;
            Website = website;
        }
    }
}
