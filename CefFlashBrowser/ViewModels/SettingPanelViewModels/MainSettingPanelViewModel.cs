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
        public DelegateCommand SwitchMainPageFunctionCommand { get; set; }

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

        private List<MainPageFunctionComboBoxViewModel> _mainPageFunctions;

        public List<MainPageFunctionComboBoxViewModel> MainPageFunctions
        {
            get => _mainPageFunctions;
            set
            {
                _mainPageFunctions = value;
                RaisePropertyChanged("MainPageFunctions");
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

        public int CurrentMainPageFunction
        {
            get => Settings.MainPageFunction;
            set
            {
                Settings.MainPageFunction = value;
                RaisePropertyChanged("CurrentMainPageFunction");
            }
        }

        private void LoadSearchEngineItems()
        {
            SearchEngineItems = new List<SearchEngineComboBoxItemViewModel>();
            foreach (var (engine, name) in SearchEngine.GetSupportedSearchEngines())
                SearchEngineItems.Add(new SearchEngineComboBoxItemViewModel(name, engine));
        }

        private void LoadMainPageFunctions()
        {
            MainPageFunctions = new List<MainPageFunctionComboBoxViewModel>();
            for (int i = 0; i < 3; i++)
                MainPageFunctions.Add(new MainPageFunctionComboBoxViewModel(i));
        }

        private void SwitchSearchEngine(object sender)
        {
            CurrentSearchEngineValue = (int)((sender as ComboBox)?.SelectedValue ?? 0);
        }

        private void SwitchMainPageFunction(object sender)
        {
            CurrentMainPageFunction = (int)((sender as ComboBox)?.SelectedValue ?? 0);
        }

        public MainSettingPanelViewModel()
        {
            LoadMainPageFunctions();
            LoadSearchEngineItems();

            SwitchMainPageFunctionCommand = new DelegateCommand(SwitchMainPageFunction);
            SwitchSearchEngineCommand = new DelegateCommand(SwitchSearchEngine);
        }
    }
}
