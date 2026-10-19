using CefSharp;
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

            SimpleIoc.Global.Register(BrowserWindowViewModelFactory, Lifetime.Transient);
            SimpleIoc.Global.Register<SolSaveManagerViewModel>(Lifetime.Transient);
        }

        private static BrowserWindowViewModel BrowserWindowViewModelFactory(object[] parameters)
        {
            return new BrowserWindowViewModel(parameters?.FirstOrDefault() as IWebBrowser);
        }

        public MainWindowViewModel MainWindowViewModel
        {
            get => SimpleIoc.Global.GetInstance<MainWindowViewModel>();
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
