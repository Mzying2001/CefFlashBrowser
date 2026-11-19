using Mzying2001.MonkeySharp.Core.Permissions;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class UserScriptPermissionDialog : Window
    {
        public UserScriptPermissionChoice Choice { get; private set; } = new UserScriptPermissionChoice(PermissionDecision.Deny);

        public UserScriptPermissionDialog()
        {
            InitializeComponent();

            PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                    Close();
            };
        }

        internal void SetDescription(string description) => message.Text = description;

        private void DenyClick(object sender, RoutedEventArgs e) => Complete(PermissionDecision.Deny, true);

        private void AllowOnceClick(object sender, RoutedEventArgs e) => Complete(PermissionDecision.Allow, false);

        private void AllowRememberClick(object sender, RoutedEventArgs e) => Complete(PermissionDecision.Allow, true);

        private void Complete(PermissionDecision decision, bool remember)
        {
            Choice = new UserScriptPermissionChoice(decision, remember);
            Close();
        }
    }
}
