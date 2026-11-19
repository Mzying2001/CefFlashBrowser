using CefFlashBrowser.Models.UserScripts;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Utils;
using Mzying2001.MonkeySharp.Core.Permissions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Views
{
    public partial class UserScriptPermissionsWindow : Window
    {
        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();
        private UserScriptService _service;
        private Uri _page;
        private bool _busy;

        public UserScriptPermissionsWindow()
        {
            InitializeComponent();
        }

        internal void SetPage(UserScriptService service, string address)
        {
            _service = service;
            Uri.TryCreate(address, UriKind.Absolute, out _page);
            pageAddress.Text = address ?? string.Empty;
        }

        private async void WindowLoaded(object sender, RoutedEventArgs e)
        {
            Loaded -= WindowLoaded;
            await RefreshAsync();
        }

        private async void RefreshClicked(object sender, RoutedEventArgs e) => await RefreshAsync();

        private async void AllowClicked(object sender, RoutedEventArgs e) => await ChangeSelectionAsync(PermissionDecision.Allow);

        private async void DenyClicked(object sender, RoutedEventArgs e) => await ChangeSelectionAsync(PermissionDecision.Deny);

        private async void ClearClicked(object sender, RoutedEventArgs e) => await ChangeSelectionAsync(null);

        private void CloseClicked(object sender, RoutedEventArgs e) => Close();

        private void PermissionSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();

        private Task ChangeSelectionAsync(PermissionDecision? decision)
        {
            var selected = permissionList.SelectedItem as UserScriptPagePermission;

            return selected == null || selected.Record.Decision == decision
                ? Task.CompletedTask
                : RefreshAsync(selected.Record, decision);
        }

        private async Task RefreshAsync(UserScriptPermissionRecord expected = null, PermissionDecision? decision = null)
        {
            if (_busy)
                return;

            var token = _lifetime.Token;
            var selectedScope = (permissionList.SelectedItem as UserScriptPagePermission)?.Record.Scope;

            _busy = true;
            UpdateButtons();
            SetStatus("userscript_permissionLoading");

            try
            {
                var stale = expected != null && !await _service.Decisions.ChangeAsync(expected, decision, token);
                var records = await _service.GetPagePermissionsAsync(_page, token);

                token.ThrowIfCancellationRequested();

                permissionList.ItemsSource = records;

                if (selectedScope != null)
                {
                    permissionList.SelectedItem = records.FirstOrDefault(item => item.Record.Scope.ScriptKey == selectedScope.ScriptKey
                        && item.Record.Scope.Kind == selectedScope.Kind && item.Record.Scope.Target == selectedScope.Target);
                }

                if (stale)
                {
                    SetStatus("userscript_permissionChanged");
                }
                else
                {
                    SetStatus(records.Count == 0 ? "userscript_permissionEmpty" : null);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
            }
            catch (Exception error)
            {
                LogHelper.LogError("Could not manage userscript permissions.", error);

                if (!token.IsCancellationRequested)
                    SetStatus("userscript_permissionError");
            }
            finally
            {
                _busy = false;

                if (!token.IsCancellationRequested)
                    UpdateButtons();
            }
        }

        private void UpdateButtons()
        {
            var selected = (permissionList.SelectedItem as UserScriptPagePermission)?.Record.Decision;

            refreshButton.IsEnabled = !_busy;
            clearButton.IsEnabled = !_busy && selected.HasValue;
            allowButton.IsEnabled = clearButton.IsEnabled && selected != PermissionDecision.Allow;
            denyButton.IsEnabled = clearButton.IsEnabled && selected != PermissionDecision.Deny;
        }

        private void SetStatus(string resource)
        {
            if (resource == null)
            {
                statusText.Visibility = Visibility.Collapsed;
                return;
            }

            statusText.SetResourceReference(TextBlock.TextProperty, resource);
            statusText.Visibility = Visibility.Visible;
        }

        protected override void OnClosed(EventArgs e)
        {
            _lifetime.Cancel();
            _lifetime.Dispose();
            base.OnClosed(e);
        }
    }
}
