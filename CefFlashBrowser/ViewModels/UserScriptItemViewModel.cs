using Mzying2001.MonkeySharp.Core.Domain;
using SimpleMvvm;
using System;
using System.Linq;

namespace CefFlashBrowser.ViewModels
{
    public class UserScriptItemViewModel : ViewModelBase
    {
        private string _source;
        private string _savedSource;

        public UserScriptInstallation Installation { get; private set; }

        public Guid EditorRevision { get; private set; }

        public string Origin { get; private set; }

        public string DraftName { get; }

        public bool RequiresConfirmation { get; }

        public bool IsInstalled => Installation != null;

        public bool IsEnabled => Installation?.IsEnabled == true;

        public bool IsReady => Installation?.IsReady == true;

        public string Name => Installation?.Definition.Metadata?.Name ?? DraftName;

        public string Version => Installation?.Definition.Metadata?.Version;

        public string Problem => Installation == null || Installation.IsReady ? null
            : string.Join(Environment.NewLine, Installation.Diagnostics.Select(item => item.Message));

        public bool IsDirty => !IsInstalled || Source != _savedSource;

        public string DirtyMark => IsDirty ? "*" : string.Empty;

        public string Source
        {
            get => _source;
            set
            {
                if (_source == value) return;

                UpdateValue(ref _source, value);
                RaisePropertyChanged(nameof(IsDirty));
                RaisePropertyChanged(nameof(DirtyMark));
            }
        }

        public UserScriptItemViewModel(string source, string origin, string name, bool requiresConfirmation)
        {
            _source = source;
            Origin = origin;
            DraftName = name;
            RequiresConfirmation = requiresConfirmation;
        }

        public UserScriptItemViewModel(UserScriptInstallation installation)
            : this(installation.Definition.Source, installation.SourceOrigin, installation.ScriptKey.ToString(), false)
        {
            AcceptSave(installation, installation.Definition.Source);
        }

        public void Synchronize(UserScriptInstallation installation, bool enabledChanged)
        {
            var clean = !IsDirty;
            var sameSource = Installation?.Definition.Source == installation.Definition.Source
                && Origin == installation.SourceOrigin;

            if (clean || (enabledChanged && sameSource && EditorRevision == Installation?.RevisionId))
                EditorRevision = installation.RevisionId;

            Installation = installation;

            if (clean)
            {
                _savedSource = installation.Definition.Source;
                Source = _savedSource;
                Origin = installation.SourceOrigin;
            }

            NotifyInstallation();
        }

        public void AcceptSave(UserScriptInstallation installation, string submittedSource)
        {
            Installation = installation;
            EditorRevision = installation.RevisionId;
            Origin = installation.SourceOrigin;
            _savedSource = submittedSource;
            NotifyInstallation();
        }

        private void NotifyInstallation()
        {
            RaisePropertyChanged(nameof(IsInstalled));
            RaisePropertyChanged(nameof(IsEnabled));
            RaisePropertyChanged(nameof(IsReady));
            RaisePropertyChanged(nameof(Name));
            RaisePropertyChanged(nameof(Version));
            RaisePropertyChanged(nameof(Problem));
            RaisePropertyChanged(nameof(IsDirty));
            RaisePropertyChanged(nameof(DirtyMark));
        }
    }
}
