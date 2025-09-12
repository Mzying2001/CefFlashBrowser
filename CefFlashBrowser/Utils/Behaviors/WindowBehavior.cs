using Microsoft.Xaml.Behaviors;
using SimpleMvvm.Command;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Utils.Behaviors
{
    public class WindowBehavior : Behavior<Window>
    {
        private ICommand _closeWindowCommand;
        public ICommand CloseWindowCommand => _closeWindowCommand;

        private ICommand _setDialogResultCommand;
        public ICommand SetDialogResultCommand => _setDialogResultCommand;

        public WindowBehavior()
        {
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            _closeWindowCommand = new DelegateCommand(AssociatedObject.Close);
            _setDialogResultCommand = new DelegateCommand<bool?>(res => DialogHelper.SetDialogResult(AssociatedObject, res));
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            _closeWindowCommand = null;
            _setDialogResultCommand = null;
        }
    }
}
