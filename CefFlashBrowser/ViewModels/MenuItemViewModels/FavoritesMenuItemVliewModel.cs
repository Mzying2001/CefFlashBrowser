using CefFlashBrowser.Models;
using CefFlashBrowser.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.MenuItemViewModels
{
    class FavoritesMenuItemVliewModel : MenuItemViewModel
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

        protected override void MenuItemSelected(object sender)
        {
            if (sender is BrowserWindow bw)
                bw.browser.Address = Website.Url;
            else
                BrowserWindow.Popup(Website.Url);
        }

        public FavoritesMenuItemVliewModel(Website website) : base()
        {
            Header = website.Name;
            Website = website;
        }
    }
}
