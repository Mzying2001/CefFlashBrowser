using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.ViewModels.ComboBoxItemViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace CefFlashBrowser.ViewModels.SettingPanelViewModels
{
    class MainSettingPanelViewModel : NotificationObject
    {
        public DelegateCommand SwitchAddressBarFunctionCommand { get; set; }

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

        private List<AddressBarFunctionComboBoxViewModel> _addressBarFunctions;

        public List<AddressBarFunctionComboBoxViewModel> AddressBarFunctions
        {
            get => _addressBarFunctions;
            set
            {
                _addressBarFunctions = value;
                RaisePropertyChanged("AddressBarFunctions");
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

        public int CurrentAddressBarFunction
        {
            get => Settings.AddressBarFunction;
            set
            {
                Settings.AddressBarFunction = value;
                RaisePropertyChanged("CurrentAddressBarFunction");
            }
        }

        private void LoadSearchEngineItems()
        {
            SearchEngineItems = new List<SearchEngineComboBoxItemViewModel>();
            foreach (var (engine, name) in SearchEngine.GetSupportedSearchEngines())
                SearchEngineItems.Add(new SearchEngineComboBoxItemViewModel(name, engine));
        }

        private void LoadAddressBarFunctions()
        {
            AddressBarFunctions = new List<AddressBarFunctionComboBoxViewModel>();
            for (int i = 0; i < 3; i++)
                AddressBarFunctions.Add(new AddressBarFunctionComboBoxViewModel(i));
        }

        private void SwitchSearchEngine(object sender)
        {
            CurrentSearchEngineValue = (int)((sender as ComboBox)?.SelectedValue ?? 0);
        }

        private void SwitchAddressBarFunction(object sender)
        {
            CurrentAddressBarFunction = (int)((sender as ComboBox)?.SelectedValue ?? 0);
        }

        public MainSettingPanelViewModel()
        {
            LoadAddressBarFunctions();
            LoadSearchEngineItems();

            SwitchAddressBarFunctionCommand = new DelegateCommand(SwitchAddressBarFunction);
            SwitchSearchEngineCommand = new DelegateCommand(SwitchSearchEngine);
        }
    }
}
