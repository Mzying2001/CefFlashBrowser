using SimpleMvvm.Ioc;

namespace CefFlashBrowser.ViewModels
{
    public class ViewModelLocator : SimpleIoc
    {
        public ViewModelLocator()
        {
            Register<MainWindowViewModel>();
            Register<BrowserWindowViewModel>();
            Register<SettingsWindowViewModel>();
            Register<FavoritesManagerViewModel>();
            Register<LanguageSelectorViewModel>();
            Register<SolSaveManagerViewModel>();
        }

        public MainWindowViewModel MainWindowViewModel
        {
            get => GetInstance<MainWindowViewModel>();
        }

        public BrowserWindowViewModel BrowserWindowViewModel
        {
            get => GetInstance<BrowserWindowViewModel>();
        }

        public SettingsWindowViewModel SettingsWindowViewModel
        {
            get => GetInstance<SettingsWindowViewModel>();
        }

        public FavoritesManagerViewModel FavoritesManagerViewModel
        {
            get => GetInstance<FavoritesManagerViewModel>();
        }

        public LanguageSelectorViewModel LanguageSelectorViewModel
        {
            get => GetInstance<LanguageSelectorViewModel>();
        }

        public SolSaveManagerViewModel SolSaveManagerViewModel
        {
            get => GetInstance<SolSaveManagerViewModel>();
        }
    }
}
