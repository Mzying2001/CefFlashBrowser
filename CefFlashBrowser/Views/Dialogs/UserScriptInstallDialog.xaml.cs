using CefFlashBrowser.ViewModels;
using Mzying2001.MonkeySharp.Core.Domain;
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

        internal void SetScript(UserScriptMetadata metadata, string origin)
        {
            scriptIcon.FaviconUrl = UserScriptItemViewModel.ResolveIconUrl(metadata, origin);
            scriptIcon.DefaultIcon = UserScriptItemViewModel.GetIconInitial(metadata.Name);
            scriptName.Text = metadata.Name;

            SetOptionalText(scriptVersion, metadata.Version);
            SetOptionalText(scriptDescription, metadata.Description);
            SetOptionalText(scriptAuthor, metadata.Author, authorLabel);
            scriptOrigin.Text = origin;

            SetOptionalText(scriptMatches, string.Join(", ", metadata.Matches.Concat(metadata.Includes)), matchesLabel);
            SetOptionalText(scriptWorld, string.Join(" / ", new[] { metadata.Sandbox, metadata.InjectInto }.Where(value => !string.IsNullOrWhiteSpace(value))), worldLabel);
            SetOptionalText(scriptGrants, string.Join(", ", metadata.DeclaredGrants), grantsLabel);
            SetOptionalText(scriptConnects, string.Join(", ", metadata.Connects), connectsLabel);
            SetOptionalText(scriptRequires, string.Join(", ", metadata.Requires.Select(item => item.Url)), requiresLabel);
            SetOptionalText(scriptResources, string.Join(", ", metadata.Resources.Select(item => item.Name + " " + item.Url)), resourcesLabel);
        }
    }
}
