using SimpleMvvm;

namespace CefFlashBrowser.ViewModels.ComboBoxItemViewModels
{
    class ComboBoxItemViewModel<K, V> : ViewModelBase
    {
        private K _displayMember;

        public K DisplayMember
        {
            get => _displayMember;
            set
            {
                _displayMember = value;
                RaisePropertyChanged("DisplayMember");
            }
        }

        private V _value;

        public V Value
        {
            get => _value;
            set
            {
                _value = value;
                RaisePropertyChanged("Value");
            }
        }

    }
}
