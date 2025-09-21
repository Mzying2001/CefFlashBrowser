using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using CefFlashBrowser.ViewModels;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CefFlashBrowser.Views
{
    public partial class SolEditorWindow : Window
    {
        public SolEditorWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel) return;

            if (DataContext is SolEditorWindowViewModel vm)
            {
                if (vm.Status != SolEditorStatus.Modified)
                {
                    return;
                }

                var message = LanguageManager.GetFormattedString("message_askSaveChange", vm.FilePath);
                var result = MessageBox.Show(message, Title, MessageBoxButton.YesNoCancel);

                if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
                else if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        vm.SaveFile();
                    }
                    catch (Exception ex)
                    {
                        WindowManager.ShowError(ex.Message);
                        e.Cancel = true;
                    }
                }
            }
        }

        private void TreeViewItemPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem item)
            {
                item.IsSelected = true;
            }
        }
    }
}
