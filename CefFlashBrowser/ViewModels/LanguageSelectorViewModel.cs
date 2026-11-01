using CefFlashBrowser.Services;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Ioc;
using System.Collections.Generic;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class LanguageSelectorViewModel : ViewModelBase
    {
        private LanguageService LanguageService
        {
            get => SimpleIoc.Global.GetInstance<LanguageService>();
        }

        public DelegateCommand SetLanguageCommand { get; set; }

        public List<LanguageItemViewModel> LanguageList { get; }

        private void SetLanguage(string language)
        {
            if (language != null)
            {
                LanguageService.SetCurrentLanguage(language);
            }
        }

        public LanguageSelectorViewModel()
        {
            LanguageList = LanguageService.GetSupportedLanguage().Select(item => new LanguageItemViewModel(item)).ToList();
            SetLanguageCommand = new DelegateCommand<string>(SetLanguage);
        }
    }
}
