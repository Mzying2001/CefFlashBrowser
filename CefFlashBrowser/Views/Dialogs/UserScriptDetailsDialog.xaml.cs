using CefFlashBrowser.ViewModels;
using Mzying2001.MonkeySharp.Core.Domain;
using Mzying2001.MonkeySharp.Core.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class UserScriptDetailsDialog : Window
    {
        public UserScriptDetailsDialog()
        {
            InitializeComponent();
        }

        internal void SetScript(UserScriptItemViewModel script)
        {
            var metadata = new UserScriptMetadataParser().Parse(script.Source ?? string.Empty).Metadata;
            var name = string.IsNullOrWhiteSpace(metadata?.Name) ? script.Name : metadata.Name;

            scriptName.Text = name;
            scriptIcon.FaviconUrl = UserScriptItemViewModel.ResolveIconUrl(metadata, script.Origin);
            scriptIcon.DefaultIcon = UserScriptItemViewModel.GetIconInitial(name);
            SetOptionalText(scriptVersion, metadata?.Version);
            SetOptionalText(scriptDescription, metadata?.Description);

            if (metadata == null)
            {
                metadataSection.Visibility = Visibility.Collapsed;
                invalidMetadata.Visibility = Visibility.Visible;
                return;
            }

            metadataRows.ItemsSource = GetMetadataRows(metadata).Where(row => row.Value != null).ToArray();

            if (metadata.AdditionalEntries.Count > 0)
            {
                unknownMetadataRows.ItemsSource = metadata.AdditionalEntries
                    .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(entry => new MetadataRow(entry.Key, JoinLines(entry.Value)))
                    .ToArray();
                unknownMetadataSection.Visibility = Visibility.Visible;
            }
        }

        private static IEnumerable<MetadataRow> GetMetadataRows(UserScriptMetadata metadata)
        {
            yield return new MetadataRow("name", metadata.Name);

            foreach (var name in metadata.LocalizedNames)
                yield return new MetadataRow("name:" + name.Key, name.Value);

            yield return new MetadataRow("namespace", metadata.Namespace);
            yield return new MetadataRow("version", metadata.Version);
            yield return new MetadataRow("description", metadata.Description);

            foreach (var description in metadata.LocalizedDescriptions)
                yield return new MetadataRow("description:" + description.Key, description.Value);

            yield return new MetadataRow("author", metadata.Author);
            yield return new MetadataRow("license", metadata.License);
            yield return new MetadataRow("copyright", metadata.Copyright);
            yield return new MetadataRow("icon", metadata.IconUrl);
            yield return new MetadataRow("icon64", metadata.Icon64Url);
            yield return new MetadataRow("homepageURL", metadata.HomepageUrl);
            yield return new MetadataRow("website", metadata.WebsiteUrl);
            yield return new MetadataRow("source", metadata.SourceUrl);
            yield return new MetadataRow("supportURL", metadata.SupportUrl);
            yield return new MetadataRow("downloadURL", metadata.DownloadUrl);
            yield return new MetadataRow("updateURL", metadata.UpdateUrl);
            yield return new MetadataRow("match", JoinLines(metadata.Matches));
            yield return new MetadataRow("include", JoinLines(metadata.Includes));
            yield return new MetadataRow("exclude-match", JoinLines(metadata.ExcludeMatches));
            yield return new MetadataRow("exclude", JoinLines(metadata.Excludes));
            yield return new MetadataRow("run-at", FormatRunAt(metadata.RunAt));
            yield return new MetadataRow("run-in", JoinLines(metadata.RunIn));
            yield return new MetadataRow("noframes", metadata.NoFrames ? string.Empty : null);
            yield return new MetadataRow("unwrap", metadata.Unwrap ? string.Empty : null);
            yield return new MetadataRow("inject-into", metadata.InjectInto);
            yield return new MetadataRow("sandbox", metadata.Sandbox);
            yield return new MetadataRow("grant", JoinLines(metadata.DeclaredGrants));
            yield return new MetadataRow("connect", JoinLines(metadata.Connects));
            yield return new MetadataRow("require", JoinLines(metadata.Requires.Select(item => FormatExternalUrl(item.Url, item.Integrity))));
            yield return new MetadataRow("resource", JoinLines(metadata.Resources.Select(item => item.Name + " " + FormatExternalUrl(item.Url, item.Integrity))));

            foreach (var group in metadata.Antifeatures.GroupBy(item => item.Locale))
            {
                var key = string.IsNullOrEmpty(group.Key) ? "antifeature" : "antifeature:" + group.Key;
                yield return new MetadataRow(key, JoinLines(group.Select(item => item.Type + " " + item.Description)));
            }

            yield return new MetadataRow("webRequest", JoinLines(metadata.WebRequest.Select(rule =>
                "{\"selector\":" + rule.Selector.GetRawText() + ",\"action\":" + rule.Action.GetRawText() + "}")));
        }

        private static void SetOptionalText(TextBlock element, string value)
        {
            element.Text = value;
            element.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
        }

        private static string JoinLines(IEnumerable<string> values)
        {
            var lines = values.ToArray();
            return lines.Length == 0 ? null : string.Join(Environment.NewLine, lines);
        }

        private static string FormatExternalUrl(string url, IReadOnlyList<ResourceIntegrityDeclaration> integrity)
        {
            return integrity.Count == 0 ? url : url + "#" +
                string.Join(",", integrity.Select(item => item.Algorithm + "=" + item.Digest));
        }

        private static string FormatRunAt(UserScriptRunAt runAt)
        {
            switch (runAt)
            {
                case UserScriptRunAt.DocumentStart: return "document-start";
                case UserScriptRunAt.DocumentBody: return "document-body";
                case UserScriptRunAt.DocumentEnd: return "document-end";
                case UserScriptRunAt.DocumentIdle: return "document-idle";
                case UserScriptRunAt.ContextMenu: return "context-menu";
                default: return runAt.ToString();
            }
        }

        private sealed class MetadataRow
        {
            public string Key { get; }
            public string Value { get; }

            public MetadataRow(string key, string value)
            {
                Key = "@" + key;
                Value = value;
            }
        }
    }
}
