using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace CefFlashBrowser.Commands
{
    class DelegateCommand : ICommand
    {
        public event EventHandler CanExecuteChanged;

        bool ICommand.CanExecute(object parameter)
        {
            return CanExecute == null || CanExecute(parameter);
        }

        void ICommand.Execute(object parameter)
        {
            Execute?.Invoke(parameter);
        }

        public Func<object, bool> CanExecute { get; set; }

        public Action<object> Execute { get; set; }

        public DelegateCommand()
        {
            //nothing to do
        }

        public DelegateCommand(Action<object> executeDelegate)
        {
            Execute = executeDelegate;
        }

        public DelegateCommand(Func<object, bool> canExecuteDelegate)
        {
            CanExecute = canExecuteDelegate;
        }

        public DelegateCommand(Action<object> executeDelegate, Func<object, bool> canExecuteDelegate)
        {
            Execute = executeDelegate;
            CanExecute = canExecuteDelegate;
        }
    }
}
