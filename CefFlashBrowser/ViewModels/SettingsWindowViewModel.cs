using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels
{
    class SettingsWindowViewModel : NotificationObject
    {
        private List<KeyValuePair<SearchEngine.Engines, string>> _searchEngineItems;

        public List<KeyValuePair<SearchEngine.Engines, string>> SearchEngineItems
        {
            get => _searchEngineItems;
            set
            {
                _searchEngineItems = value;
                RaisePropertyChanged("SearchEngineItems");
            }
        }

        private void LoadSearchEngines()
        {
            SearchEngineItems = new List<KeyValuePair<SearchEngine.Engines, string>>();
            foreach (var (engine, name) in SearchEngine.GetSupportedSearchEngines())
                SearchEngineItems.Add(new KeyValuePair<SearchEngine.Engines, string>(engine, name));
        }

        public SettingsWindowViewModel()
        {
            LoadSearchEngines();
        }
    }
}
