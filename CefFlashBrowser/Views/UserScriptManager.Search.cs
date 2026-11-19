using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CefFlashBrowser.Views
{
    public partial class UserScriptManager
    {
        private bool SearchIsOpen => searchBar?.Visibility == Visibility.Visible;
        private bool CanSearch => sourceEditor != null && scriptList?.SelectedItem != null;

        private void CanFindOrReplace(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = CanSearch;
            e.Handled = true;
        }

        private void OpenFindOrReplace(object sender, ExecutedRoutedEventArgs e)
        {
            if (!CanSearch)
                return;

            if (!SearchIsOpen && !sourceEditor.TextArea.Selection.IsMultiline && sourceEditor.SelectionLength > 0)
                searchText.Text = sourceEditor.SelectedText;

            searchBar.Visibility = Visibility.Visible;
            replaceRow.Visibility = e.Command == ApplicationCommands.Replace ? Visibility.Visible : Visibility.Collapsed;

            ApplySearchQuery();

            searchText.Focus();
            searchText.SelectAll();
            e.Handled = true;
        }

        private void OnSearchScriptChanged()
        {
            if (SearchIsOpen && !CanSearch)
                CloseSearch(restoreFocus: false);

            CommandManager.InvalidateRequerySuggested();
        }

        private void CloseSearch(bool restoreFocus)
        {
            searchBar.Visibility = Visibility.Collapsed;
            sourceEditor.Search.SetQuery(string.Empty, matchCase: false);
            if (restoreFocus && CanSearch) sourceEditor.Focus();
        }

        private void ApplySearchQuery()
        {
            if (!SearchIsOpen || !CanSearch) return;

            sourceEditor.Search.SetQuery(searchText.Text, matchCase.IsChecked == true);
            UpdateSearchControls();
            // The toolbar can change the editor viewport when it opens or its results wrap.
            sourceEditor.UpdateLayout();
            sourceEditor.Search.Find(forward: true, includeCurrent: true);
        }

        private void UpdateSearchControls()
        {
            if (!SearchIsOpen) return;

            int count = sourceEditor.Search.Count;
            bool hasMatches = count > 0;
            findPrevious.IsEnabled = hasMatches;
            findNext.IsEnabled = hasMatches;
            replaceCurrent.IsEnabled = sourceEditor.Search.HasCurrentMatch;
            replaceAll.IsEnabled = hasMatches;
            matchCount.Visibility = hasMatches ? Visibility.Visible : Visibility.Collapsed;
            matchCountValue.Text = count.ToString();
            noMatches.Visibility = !hasMatches && searchText.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SearchPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!SearchIsOpen) return;

            var modifiers = e.KeyboardDevice.Modifiers;

            if (e.Key == Key.Escape && modifiers == ModifierKeys.None)
            {
                CloseSearch(restoreFocus: true);
                e.Handled = true;
            }
            else if ((e.Key == Key.F3 || (e.Key == Key.Enter && searchText.IsKeyboardFocusWithin)) &&
                (modifiers == ModifierKeys.None || modifiers == ModifierKeys.Shift))
            {
                sourceEditor.Search.Find(forward: modifiers == ModifierKeys.None);
                e.Handled = true;
            }
        }

        private void SearchTextChanged(object sender, TextChangedEventArgs e) => ApplySearchQuery();

        private void MatchCaseChanged(object sender, RoutedEventArgs e) => ApplySearchQuery();

        private void FindMatchClicked(object sender, RoutedEventArgs e) => sourceEditor.Search.Find(forward: sender == findNext);

        private void CloseSearchClicked(object sender, RoutedEventArgs e) => CloseSearch(restoreFocus: true);

        private void ReplaceCurrentClicked(object sender, RoutedEventArgs e) => sourceEditor.Search.ReplaceCurrent(replacementText.Text);

        private void ReplaceAllClicked(object sender, RoutedEventArgs e) => sourceEditor.Search.ReplaceAll(replacementText.Text);
    }
}
