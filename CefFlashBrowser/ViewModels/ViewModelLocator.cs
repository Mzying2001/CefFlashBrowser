using SimpleMvvm.Ioc;
using System;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class ViewModelLocator
    {
        private static readonly object _lock = new object();

        public ViewModelLocator()
        {
            lock (_lock)
            {
                Initialize();
            }
        }

        private void Initialize()
        {
            if (SimpleIoc.Global.IsRegistered<ViewModelLocator>())
            {
                throw new InvalidOperationException("ViewModelLocator has already been initialized.");
            }

            SimpleIoc.Global.Register(this);

            SimpleIoc.Global.Register<MainWindowViewModel>();
            SimpleIoc.Global.Register<SettingsWindowViewModel>();
            SimpleIoc.Global.Register<FavoritesManagerViewModel>();
            SimpleIoc.Global.Register<LanguageSelectorViewModel>();

            SimpleIoc.Global.Register(BrowserViewModelFactory, Lifetime.Transient);
            SimpleIoc.Global.Register<BrowserWindowViewModel>(Lifetime.Transient);
            SimpleIoc.Global.Register<SolSaveManagerViewModel>(Lifetime.Transient);
        }

        private static BrowserViewModel BrowserViewModelFactory(object[] parameters)
        {
            var viewModel = new BrowserViewModel();

            if (parameters?.FirstOrDefault() is string address)
                viewModel.Address = address;

            return viewModel;
        }

        public MainWindowViewModel MainWindowViewModel
        {
            get => SimpleIoc.Global.GetInstance<MainWindowViewModel>();
        }

        public BrowserWindowViewModel BrowserWindowViewModel
        {
            get => SimpleIoc.Global.GetInstance<BrowserWindowViewModel>();
        }

        public SettingsWindowViewModel SettingsWindowViewModel
        {
            get => SimpleIoc.Global.GetInstance<SettingsWindowViewModel>();
        }

        public FavoritesManagerViewModel FavoritesManagerViewModel
        {
            get => SimpleIoc.Global.GetInstance<FavoritesManagerViewModel>();
        }

        public LanguageSelectorViewModel LanguageSelectorViewModel
        {
            get => SimpleIoc.Global.GetInstance<LanguageSelectorViewModel>();
        }

        public SolSaveManagerViewModel SolSaveManagerViewModel
        {
            get => SimpleIoc.Global.GetInstance<SolSaveManagerViewModel>();
        }
    }
}
