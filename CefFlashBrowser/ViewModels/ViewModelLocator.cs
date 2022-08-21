using SimpleMvvm.Locator;

namespace CefFlashBrowser.ViewModels
{
    public class ViewModelLocator : ViewModelLocatorBase
    {
        public ViewModelLocator()
        {
            Register<MainWindowViewModel>();
            Register<BrowserWindowViewModel>();
            Register<SettingsWindowViewModel>();
            Register<FavoritesManagerViewModel>();
            Register<LanguageSelectorViewModel>();
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
    }
}
