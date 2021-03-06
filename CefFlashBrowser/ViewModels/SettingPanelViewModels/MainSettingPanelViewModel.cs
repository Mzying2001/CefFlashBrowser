﻿using CefFlashBrowser.Commands;
using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using CefFlashBrowser.ViewModels.ComboBoxItemViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace CefFlashBrowser.ViewModels.SettingPanelViewModels
{
    class MainSettingPanelViewModel : NotificationObject
    {
        public ICommand SwitchAddressBarFunctionCommand { get; set; }
        public ICommand SwitchSearchEngineCommand { get; set; }

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
