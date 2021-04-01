using CefFlashBrowser.Models;
using CefFlashBrowser.Models.StaticData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.ComboBoxItemViewModels
{
    class AddressBarFunctionComboBoxViewModel : ComboBoxItemViewModel<string, int>
    {
        public AddressBarFunctionComboBoxViewModel(int func)
        {
            Value = func;
            switch (func)
            {
                case 0:
                    DisplayMember = LanguageManager.GetString("addressBarFunction_auto");
                    break;

                case 1:
                    DisplayMember = LanguageManager.GetString("addressBarFunction_searchOnly");
                    break;

                case 2:
                    DisplayMember = LanguageManager.GetString("addressBarFunction_navigateOnly");
                    break;

                default:
                    throw new IndexOutOfRangeException();
            }
        }
    }
}
