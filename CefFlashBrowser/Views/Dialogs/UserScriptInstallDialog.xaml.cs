using CefFlashBrowser.ViewModels;
using Mzying2001.MonkeySharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class UserScriptInstallDialog : Window
    {
        public UserScriptInstallDialog()
        {
            InitializeComponent();
        }

        private static void SetOptionalText(TextBlock element, string value, TextBlock label = null)
        {
            element.Text = value;
            element.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

            if (label != null)
            {
                label.Visibility = element.Visibility;
            }
        }

        private static string JoinLines(IEnumerable<string> entries)
        {
            return string.Join(Environment.NewLine, entries.Where(entry => !string.IsNullOrWhiteSpace(entry)));
        }

        internal void SetScript(UserScriptMetadata metadata, string origin, bool update, string warning = null)
        {
            scriptIcon.FaviconUrl = UserScriptItemViewModel.ResolveIconUrl(metadata, origin);
            scriptIcon.DefaultIcon = UserScriptItemViewModel.GetIconInitial(metadata.Name);
            scriptName.Text = metadata.Name;

            SetOptionalText(scriptVersion, metadata.Version);
            SetOptionalText(scriptDescription, metadata.Description);
            SetOptionalText(updateWarning, warning);
            SetOptionalText(scriptAuthor, metadata.Author, authorLabel);
            scriptOrigin.Text = origin;

            SetOptionalText(scriptMatches, JoinLines(metadata.Matches.Concat(metadata.Includes)), matchesLabel);
            SetOptionalText(scriptWorld, JoinLines(new[] { metadata.Sandbox, metadata.InjectInto }), worldLabel);
            SetOptionalText(scriptGrants, JoinLines(metadata.DeclaredGrants), grantsLabel);
            SetOptionalText(scriptConnects, JoinLines(metadata.Connects), connectsLabel);
            SetOptionalText(scriptRequires, JoinLines(metadata.Requires.Select(item => item.Url)), requiresLabel);
            SetOptionalText(scriptResources, JoinLines(metadata.Resources.Select(item => item.Name + " " + item.Url)), resourcesLabel);

            if (update)
            {
                SetResourceReference(TitleProperty, "userScriptInstall_confirmUpdate");
                installButton.SetResourceReference(ContentControl.ContentProperty, "userScriptInstall_update");
            }
        }
    }
}
