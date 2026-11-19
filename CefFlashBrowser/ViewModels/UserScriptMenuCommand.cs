using SimpleMvvm;
using SimpleMvvm.Command;
using System;

namespace CefFlashBrowser.ViewModels
{
    public sealed class UserScriptMenuCommand : ViewModelBase
    {
        public string ScriptKey { get; }

        public string ScriptName { get; }

        public string Name { get; }

        public string Description { get; }

        public string ScriptIconUrl { get; }

        public string ScriptIconInitial => UserScriptItemViewModel.GetIconInitial(ScriptName);

        public DelegateCommand InvokeCommand { get; }

        private bool _staysOpenOnClick;
        public bool StaysOpenOnClick
        {
            get => _staysOpenOnClick;
            internal set => UpdateValue(ref _staysOpenOnClick, value);
        }

        public UserScriptMenuCommand(string scriptKey, string scriptName, string name, string description,
            Action<UserScriptMenuCommand> invoke, string scriptIconUrl = null)
        {
            ScriptKey = scriptKey;
            ScriptName = scriptName;
            Name = name;
            Description = description;
            ScriptIconUrl = scriptIconUrl;
            InvokeCommand = new DelegateCommand(() => invoke(this));
        }
    }
}
