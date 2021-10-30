using CefFlashBrowser.Models;

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
