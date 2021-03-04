using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.ComboBoxItemViewModels
{
    class MainPageFunctionComboBoxViewModel : ComboBoxItemViewModel<string, int>
    {
        public MainPageFunctionComboBoxViewModel(int func)
        {
            Value = func;
            switch (func)
            {
                case 0:
                    DisplayMember = LanguageManager.GetString("mainPageFunction_auto");
                    break;

                case 1:
                    DisplayMember = LanguageManager.GetString("mainPageFunction_searchOnly");
                    break;

                case 2:
                    DisplayMember = LanguageManager.GetString("mainPageFunction_navigateOnly");
                    break;

                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }
}
