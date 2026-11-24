using CefFlashBrowser.Data;
using CefFlashBrowser.Services;
using SimpleMvvm.Ioc;
using System;
using System.Linq;
using System.Threading.Tasks;

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
            SimpleIoc.Global.Register(UserScriptManagerViewModelFactory);

            SimpleIoc.Global.Register(BrowserViewModelFactory, Lifetime.Transient);
            SimpleIoc.Global.Register<BrowserWindowViewModel>(Lifetime.Transient);
            SimpleIoc.Global.Register<SolSaveManagerViewModel>(Lifetime.Transient);
        }

        private static UserScriptManagerViewModel UserScriptManagerViewModelFactory(object[] parameters)
        {
            async Task<Mzying2001.MonkeySharp.Core.Repository.IUserScriptRepository> initialize()
            {
                var service = SimpleIoc.Global.GetInstance<Services.UserScripts.UserScriptService>();
                await service.InitializeAsync();
                return service.Repository;
            }

            var viewModel = new UserScriptManagerViewModel(
                initialize,
                key => SimpleIoc.Global.GetInstance<LanguageService>().GetString(key),
                action => System.Windows.Application.Current.Dispatcher.InvokeAsync(action),
                (url, token) => SimpleIoc.Global.GetInstance<Services.UserScripts.UserScriptService>().Content.FetchSourceAsync(null, url, token),
                () => GlobalData.Settings.ScriptEditorSettings);

            return viewModel;
        }

        private static BrowserViewModel BrowserViewModelFactory(object[] parameters)
        {
            var viewModel = new BrowserViewModel();

            if (parameters?.FirstOrDefault() is string address)
                viewModel.Address = address;

            return viewModel;
        }

        public LanguageService LanguageService
        {
            get => SimpleIoc.Global.GetInstance<LanguageService>();
        }

        public FaviconService FaviconService
        {
            get => SimpleIoc.Global.GetInstance<FaviconService>();
        }

        public FavoritesService FavoritesService
        {
            get => SimpleIoc.Global.GetInstance<FavoritesService>();
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

        public UserScriptManagerViewModel UserScriptManagerViewModel
        {
            get => SimpleIoc.Global.GetInstance<UserScriptManagerViewModel>();
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
