using SimpleMvvm.Ioc;

namespace CefFlashBrowser.ViewModels
{
    public class ViewModelLocator
    {
        static ViewModelLocator()
        {
            SimpleIoc.Global.Register<MainWindowViewModel>();
            SimpleIoc.Global.Register<BrowserWindowViewModel>(Lifetime.Transient);
            SimpleIoc.Global.Register<SettingsWindowViewModel>();
            SimpleIoc.Global.Register<FavoritesManagerViewModel>();
            SimpleIoc.Global.Register<LanguageSelectorViewModel>();
            SimpleIoc.Global.Register<SolSaveManagerViewModel>();
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
