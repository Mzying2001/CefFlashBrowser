using CefFlashBrowser.Services;
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

        internal static UserScriptInstallDialog Create(UserScriptMetadata metadata, string origin, bool update = false)
        {
            var dialog = WindowManager.CreateWindow<UserScriptInstallDialog>();
            dialog.SetScript(metadata, origin);

            if (update)
            {
                dialog.SetResourceReference(TitleProperty, "userScriptInstall_confirmUpdate");
                dialog.installButton.SetResourceReference(ContentControl.ContentProperty, "userScriptInstall_update");
            }

            return dialog;
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

        private void SetScript(UserScriptMetadata metadata, string origin)
        {
            scriptIcon.FaviconUrl = UserScriptItemViewModel.ResolveIconUrl(metadata, origin);
            scriptIcon.DefaultIcon = UserScriptItemViewModel.GetIconInitial(metadata.Name);
            scriptName.Text = metadata.Name;

            SetOptionalText(scriptVersion, metadata.Version);
            SetOptionalText(scriptDescription, metadata.Description);
            SetOptionalText(scriptAuthor, metadata.Author, authorLabel);
            scriptOrigin.Text = origin;

            SetOptionalText(scriptMatches, JoinLines(metadata.Matches.Concat(metadata.Includes)), matchesLabel);
            SetOptionalText(scriptWorld, JoinLines(new[] { metadata.Sandbox, metadata.InjectInto }), worldLabel);
            SetOptionalText(scriptGrants, JoinLines(metadata.DeclaredGrants), grantsLabel);
            SetOptionalText(scriptConnects, JoinLines(metadata.Connects), connectsLabel);
            SetOptionalText(scriptRequires, JoinLines(metadata.Requires.Select(item => item.Url)), requiresLabel);
            SetOptionalText(scriptResources, JoinLines(metadata.Resources.Select(item => item.Name + " " + item.Url)), resourcesLabel);
        }
    }
}
