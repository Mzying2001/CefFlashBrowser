using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using NumericUpDown = HandyControl.Controls.NumericUpDown;

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
            var uiCulture = CultureInfo.CurrentUICulture;

            var families = Fonts.SystemFontFamilies
                .Select(family => new { family.Source, DisplayName = GetFontDisplayName(family, uiCulture) })
                .OrderBy(option => option.DisplayName, StringComparer.Create(uiCulture, ignoreCase: true))
                .ThenBy(option => option.Source, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            fontFamilyBox.ItemsSource = families;
            fontFamilyBox.SelectedValue = (families.FirstOrDefault(option =>
                string.Equals(option.Source, activeFontFamily, StringComparison.OrdinalIgnoreCase)) ?? families.FirstOrDefault())?.Source;

            fontSizeBox.Value = settings.FontSize;
            indentationBox.SelectedItem = settings.IndentWithSpaces ? spacesOption : tabsOption;
            tabWidthBox.Value = settings.TabWidth;
            lineNumbersBox.IsChecked = settings.ShowLineNumbers;
            wordWrapBox.IsChecked = settings.WordWrap;
        }

        private static string GetFontDisplayName(FontFamily family, CultureInfo uiCulture)
        {
            var names = family.FamilyNames;

            for (var culture = uiCulture; culture.Name.Length != 0; culture = culture.Parent)
            {
                var languageTag = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
                if (names.TryGetValue(languageTag, out var name) && !string.IsNullOrWhiteSpace(name))
                    return name;
            }

            // Use the nearest language ancestor for regional variants so zh-Hans and zh-Hant stay distinct.
            var language = uiCulture;

            while (!language.IsNeutralCulture && language.Name.Length != 0)
                language = language.Parent;

            if (language.Name.Length != 0)
            {
                foreach (var entry in names.OrderBy(entry => entry.Key.IetfLanguageTag, StringComparer.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(entry.Value) && MatchesLanguage(entry.Key, language))
                        return entry.Value;
                }
            }

            return family.Source;
        }

        private static bool MatchesLanguage(XmlLanguage tag, CultureInfo language)
        {
            CultureInfo culture;

            try
            {
                culture = tag.GetEquivalentCulture();
            }
            catch (InvalidOperationException)
            {
                // A font may carry a language tag that .NET doesn't recognize.
                return false;
            }

            for (; culture.Name.Length != 0; culture = culture.Parent)
            {
                if (culture.Equals(language))
                    return true;
            }

            return false;
        }

        private void StepNumericInput(object sender, KeyEventArgs e)
        {
            if (!(e.OriginalSource is TextBox input) || (e.Key != Key.Up && e.Key != Key.Down))
                return;

            e.Handled = true;

            // HandyControl leaves the focused text unchanged after stepping and reads it again on blur.
            var box = (NumericUpDown)sender;
            var step = e.Key == Key.Up ? box.Increment : -box.Increment;
            box.Value = Math.Round(box.Value, MidpointRounding.AwayFromZero) + step;
            input.Text = box.Value.ToString(CultureInfo.CurrentCulture);
            input.CaretIndex = input.Text.Length;
        }

        private void Accept(object sender, RoutedEventArgs e)
        {
            if (!TryReadInteger(fontSizeBox, "scriptEditorSettings_invalidFontSize", out var fontSize) ||
                !TryReadInteger(tabWidthBox, "scriptEditorSettings_invalidTabWidth", out var tabWidth))
                return;

            Result = new EditorSettings
            {
                FontFamily = fontFamilyBox.SelectedValue as string ?? EditorSettings.DefaultFontFamily,
                FontSize = fontSize,
                IndentWithSpaces = indentationBox.SelectedItem == spacesOption,
                TabWidth = tabWidth,
                ShowLineNumbers = lineNumbersBox.IsChecked == true,
                WordWrap = wordWrapBox.IsChecked == true
            };
            DialogHelper.SetDialogResult(this, true);
        }

        private bool TryReadInteger(NumericUpDown box, string messageKey, out int value)
        {
            // The pending text can be invalid while the control still holds its last valid value.
            var input = box.Template?.FindName("PART_TextBox", box) as TextBox;
            value = (int)box.Value;

            if (box.Value == value && (input == null ||
                int.TryParse(input.Text, out var typed) && typed >= box.Minimum && typed <= box.Maximum))
                return true;

            validationMessage.SetResourceReference(TextBlock.TextProperty, messageKey);
            validationMessage.Visibility = Visibility.Visible;
            input?.Focus();
            input?.SelectAll();
            return false;
        }

        private void Cancel(object sender, RoutedEventArgs e)
        {
            DialogHelper.SetDialogResult(this, false);
        }
    }
}
