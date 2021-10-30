using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.ViewModels.ComboBoxItemViewModels;
using SimpleMvvm;
using SimpleMvvm.Command;
using System;
using System.Collections.ObjectModel;

namespace CefFlashBrowser.ViewModels.SettingPanelViewModels
{
    class MainSettingPanelViewModel : ViewModelBase
    {
        public DelegateCommand SwitchAddressBarFunctionCommand { get; set; }
        public DelegateCommand SwitchSearchEngineCommand { get; set; }

        public ObservableCollection<SearchEngineComboBoxItemViewModel> SearchEngineItems { get; set; }
        public ObservableCollection<AddressBarFunctionComboBoxViewModel> AddressBarFunctions { get; set; }

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
            SearchEngineItems = new ObservableCollection<SearchEngineComboBoxItemViewModel>();
            foreach (var (engine, name) in SearchEngine.GetSupportedSearchEngines())
                SearchEngineItems.Add(new SearchEngineComboBoxItemViewModel(name, engine));
        }

        private void LoadAddressBarFunctions()
        {
            AddressBarFunctions = new ObservableCollection<AddressBarFunctionComboBoxViewModel>();
            for (int i = 0; i < 3; i++)
                AddressBarFunctions.Add(new AddressBarFunctionComboBoxViewModel(i));
        }

        private void SwitchSearchEngine(int value)
        {
            CurrentSearchEngineValue = value;
        }

        private void SwitchAddressBarFunction(int value)
        {
            CurrentAddressBarFunction = value;
        }

        public MainSettingPanelViewModel()
        {
            LoadAddressBarFunctions();
            LoadSearchEngineItems();

            SwitchAddressBarFunctionCommand = new DelegateCommand(p => SwitchAddressBarFunction(Convert.ToInt32(p)));
            SwitchSearchEngineCommand = new DelegateCommand(p => SwitchSearchEngine(Convert.ToInt32(p)));
        }
    }
}
