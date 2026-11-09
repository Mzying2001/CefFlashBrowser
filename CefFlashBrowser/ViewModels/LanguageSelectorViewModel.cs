using CefFlashBrowser.Services;
using SimpleMvvm;
using SimpleMvvm.Command;
using SimpleMvvm.Ioc;
using System.Collections.Generic;

namespace CefFlashBrowser.ViewModels
{
    public class LanguageSelectorViewModel : ViewModelBase
    {
        private LanguageService LanguageService
        {
            get => SimpleIoc.Global.GetInstance<LanguageService>();
        }

        public DelegateCommand SetLanguageCommand { get; set; }

        public ICollection<string> LanguageList { get; }

        public LanguageSelectorViewModel()
        {
            LanguageList = LanguageService.GetSupportedLanguage();
            SetLanguageCommand = new DelegateCommand<string>(SetLanguage);
        }

        private void SetLanguage(string language)
        {
            if (language != null)
            {
                LanguageService.SetCurrentLanguage(language);
            }
        }
    }
}
