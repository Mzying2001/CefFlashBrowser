using CefFlashBrowser.Commands;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CefFlashBrowser.ViewModels.MenuItemViewModels
{
    abstract class MenuItemViewModel : NotificationObject
    {
        public DelegateCommand MenuItemSelectedCommand { get; set; }

        private string _header;

        public string Header
        {
            get => _header;
            set
            {
                _header = value;
                RaisePropertyChanged("Header");
            }
        }

        private bool _isSelected;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                RaisePropertyChanged("IsSelected");
            }
        }

        protected abstract void MenuItemSelected();

        public MenuItemViewModel()
        {
            MenuItemSelectedCommand = new DelegateCommand(p => MenuItemSelected());
        }
    }
}
