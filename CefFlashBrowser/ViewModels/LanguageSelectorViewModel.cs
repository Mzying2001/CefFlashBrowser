using CefFlashBrowser.Utils;
using SimpleMvvm;
using SimpleMvvm.Command;
using System.Collections.Generic;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class LanguageSelectorViewModel : ViewModelBase
    {
        public DelegateCommand SetLanguageCommand { get; set; }

        public List<LanguageItemViewModel> LanguageList { get; }
            = LanguageManager.GetSupportedLanguage().Select(item => new LanguageItemViewModel(item)).ToList();

        private void SetLanguage(string language)
        {
            LanguageManager.CurrentLanguage = language;
        }

        public LanguageSelectorViewModel()
        {
            SetLanguageCommand = new DelegateCommand<string>(SetLanguage);
        }
    }
}
