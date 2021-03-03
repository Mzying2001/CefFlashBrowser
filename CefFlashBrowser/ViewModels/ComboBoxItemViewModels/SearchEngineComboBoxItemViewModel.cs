using CefFlashBrowser.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.ComboBoxItemViewModels
{
    class SearchEngineComboBoxItemViewModel : ComboBoxItemViewModel<string, SearchEngine.Engine>
    {
        public SearchEngineComboBoxItemViewModel(string name, SearchEngine.Engine engine)
        {
            DisplayMember = name;
            Value = engine;
        }
    }
}
