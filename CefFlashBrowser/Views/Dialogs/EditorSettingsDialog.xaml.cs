using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CefFlashBrowser.Views.Dialogs
{
    public partial class EditorSettingsDialog : Window
    {
        internal EditorSettings Result { get; private set; }

        public EditorSettingsDialog()
        {
            InitializeComponent();
        }

        internal void Initialize(EditorSettings settings, string activeFontFamily)
        {
            var families = Fonts.SystemFontFamilies
                .Select(family => family.Source)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            fontFamilyBox.ItemsSource = families;
            fontFamilyBox.SelectedItem = families.FirstOrDefault(name =>
                string.Equals(name, activeFontFamily, StringComparison.OrdinalIgnoreCase)) ?? families.FirstOrDefault();
            fontSizeBox.Text = settings.FontSize.ToString();
            indentationBox.SelectedItem = settings.IndentWithSpaces ? spacesOption : tabsOption;
            tabWidthBox.Text = settings.TabWidth.ToString();
            lineNumbersBox.IsChecked = settings.ShowLineNumbers;
            wordWrapBox.IsChecked = settings.WordWrap;
        }

        private void Accept(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(fontSizeBox.Text, out var fontSize) || !EditorSettings.IsFontSizeValid(fontSize))
            {
                ShowValidationError(fontSizeBox, "scriptEditorSettings_invalidFontSize");
                return;
            }

            if (!int.TryParse(tabWidthBox.Text, out var tabWidth) || !EditorSettings.IsTabWidthValid(tabWidth))
            {
                ShowValidationError(tabWidthBox, "scriptEditorSettings_invalidTabWidth");
                return;
            }

            Result = new EditorSettings
            {
                FontFamily = fontFamilyBox.SelectedItem as string ?? EditorSettings.DefaultFontFamily,
                FontSize = fontSize,
                IndentWithSpaces = indentationBox.SelectedItem == spacesOption,
                TabWidth = tabWidth,
                ShowLineNumbers = lineNumbersBox.IsChecked == true,
                WordWrap = wordWrapBox.IsChecked == true
            };
            DialogHelper.SetDialogResult(this, true);
        }

        private void ShowValidationError(TextBox textBox, string messageKey)
        {
            validationMessage.SetResourceReference(TextBlock.TextProperty, messageKey);
            validationMessage.Visibility = Visibility.Visible;
            textBox.Focus();
            textBox.SelectAll();
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            DialogHelper.SetDialogResult(this, false);
        }
    }
}
