using SimpleMvvm;
using SimpleMvvm.Command;
using System;

namespace CefFlashBrowser.ViewModels
{
    public sealed class UserScriptMenuCommand : ViewModelBase
    {
        public string Name { get; }
        public string Description { get; }
        public DelegateCommand InvokeCommand { get; }
        private bool _staysOpenOnClick;
        public bool StaysOpenOnClick
        {
            get => _staysOpenOnClick;
            internal set => UpdateValue(ref _staysOpenOnClick, value);
        }

        public UserScriptMenuCommand(string name, string description, Action<UserScriptMenuCommand> invoke)
        {
            Name = name;
            Description = description;
            InvokeCommand = new DelegateCommand(() => invoke(this));
        }
    }
}
