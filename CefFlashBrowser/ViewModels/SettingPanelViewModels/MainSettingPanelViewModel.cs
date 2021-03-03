using CefFlashBrowser.Models;
using CefFlashBrowser.ViewModels.ComboBoxItemViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.SettingPanelViewModels
{
    class MainSettingPanelViewModel : NotificationObject
    {
        private List<SearchEngineComboBoxItemViewModel> _searchEngineItems;

        public List<SearchEngineComboBoxItemViewModel> SearchEngineItems
        {
            get => _searchEngineItems;
            set
            {
                _searchEngineItems = value;
                RaisePropertyChanged("SearchEngineItems");
            }
        }

        private void LoadSearchEngineItems()
        {
            SearchEngineItems = new List<SearchEngineComboBoxItemViewModel>();
            foreach (var (engine, name) in SearchEngine.GetSupportedSearchEngines())
                SearchEngineItems.Add(new SearchEngineComboBoxItemViewModel(name, engine));
        }

        public void SwitchSearchEngine(SearchEngine.Engine engine)
        {
            Settings.SearchEngine = engine;
        }

        public MainSettingPanelViewModel()
        {
            LoadSearchEngineItems();
        }
    }
}
