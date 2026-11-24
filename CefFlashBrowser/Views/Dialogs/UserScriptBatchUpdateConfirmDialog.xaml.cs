using CefFlashBrowser.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class UserScriptBatchUpdateConfirmDialog : Window
    {
        private IReadOnlyList<UserScriptUpdateConfirmationEntry> _entries;

        public UserScriptBatchUpdateConfirmDialog()
        {
            InitializeComponent();
        }

        internal void SetEntries(IReadOnlyList<UserScriptUpdateConfirmationEntry> entries)
        {
            _entries = entries;
            DataContext = entries;

            foreach (var entry in entries)
                entry.PropertyChanged += EntryPropertyChanged;
            UpdateButtonState();
        }

        private void EntryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserScriptUpdateConfirmationEntry.IsSelected))
                UpdateButtonState();
        }

        private void UpdateButtonState()
        {
            updateButton.IsEnabled = _entries.Any(entry => entry.IsSelected);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (_entries != null)
            {
                foreach (var entry in _entries)
                    entry.PropertyChanged -= EntryPropertyChanged;
            }
            base.OnClosed(e);
        }
    }
}
