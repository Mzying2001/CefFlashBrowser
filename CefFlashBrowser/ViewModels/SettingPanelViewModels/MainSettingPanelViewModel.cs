using CefFlashBrowser.Commands;
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
        public DelegateCommand SwitchSearchEngineCommand { get; set; }

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

        public int CurrentSearchEngineValue
        {
            get => (int)Settings.SearchEngine;
            set
            {
                Settings.SearchEngine = (SearchEngine.Engine)value;
                RaisePropertyChanged("CurrentSearchEngineValue");
            }
        }

        private void LoadSearchEngineItems()
        {
            SearchEngineItems = new List<SearchEngineComboBoxItemViewModel>();
            foreach (var (engine, name) in SearchEngine.GetSupportedSearchEngines())
                SearchEngineItems.Add(new SearchEngineComboBoxItemViewModel(name, engine));
        }

        public void SwitchSearchEngine(object sender)
        {
            CurrentSearchEngineValue = (int)((sender as System.Windows.Controls.ComboBox)?.SelectedValue ?? 0);
        }

        public MainSettingPanelViewModel()
        {
            LoadSearchEngineItems();

            SwitchSearchEngineCommand = new DelegateCommand(SwitchSearchEngine);
        }
    }
}
