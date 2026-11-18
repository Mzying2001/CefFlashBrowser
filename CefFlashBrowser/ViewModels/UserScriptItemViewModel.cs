using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Parsing;
using SimpleMvvm;
using System;
using System.Globalization;
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

        public string Name => string.IsNullOrWhiteSpace(Installation?.Definition.Metadata?.Name)
            ? DraftName : Installation.Definition.Metadata.Name;

        public string Version => Installation?.Definition.Metadata?.Version;

        public string Problem => Installation == null || Installation.IsReady ? null
            : Installation.Diagnostics.FirstOrDefault()?.Message;

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

        public string IconUrl { get; private set; }

        public string IconInitial => GetIconInitial(Name);

        public UserScriptItemViewModel(string source, string origin, string name, bool requiresConfirmation)
        {
            _source = source;
            Origin = origin;
            DraftName = name;
            RequiresConfirmation = requiresConfirmation;
            IconUrl = ResolveIconUrl(new UserScriptMetadataParser().Parse(source).Metadata, origin);
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

        public void RefreshEnabled()
        {
            RaisePropertyChanged(nameof(IsEnabled));
        }

        private void NotifyInstallation()
        {
            IconUrl = ResolveIconUrl(Installation.Definition.Metadata, Installation.SourceOrigin);

            RaisePropertyChanged(nameof(IsInstalled));
            RaisePropertyChanged(nameof(IsEnabled));
            RaisePropertyChanged(nameof(IsReady));
            RaisePropertyChanged(nameof(Name));
            RaisePropertyChanged(nameof(Version));
            RaisePropertyChanged(nameof(Problem));
            RaisePropertyChanged(nameof(IsDirty));
            RaisePropertyChanged(nameof(DirtyMark));
            RaisePropertyChanged(nameof(IconUrl));
            RaisePropertyChanged(nameof(IconInitial));
        }

        internal static string GetIconInitial(string name)
        {
            name = name?.Trim();
            return string.IsNullOrEmpty(name) ? "?" : StringInfo.GetNextTextElement(name);
        }

        internal static string ResolveIconUrl(UserScriptMetadata metadata, string origin)
        {
            var icon = string.IsNullOrWhiteSpace(metadata?.IconUrl) ? metadata?.Icon64Url : metadata.IconUrl;
            if (string.IsNullOrWhiteSpace(icon)) return null;

            icon = icon.Trim();
            Uri uri;

            if (Uri.TryCreate(icon, UriKind.Relative, out var relative))
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var source)
                    || !Uri.TryCreate(source, relative, out uri)) return null;
            }
            else if (!Uri.TryCreate(icon, UriKind.Absolute, out uri))
            {
                return null;
            }

            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.IsFile
                ? uri.AbsoluteUri : null;
        }
    }
}
