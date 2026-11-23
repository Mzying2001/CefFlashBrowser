using CefFlashBrowser.Models;
using CefFlashBrowser.Utils;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
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
            var uiCulture = CultureInfo.CurrentUICulture;

            var families = Fonts.SystemFontFamilies
                .Select(family => new { family.Source, DisplayName = GetFontDisplayName(family, uiCulture) })
                .OrderBy(option => option.DisplayName, StringComparer.Create(uiCulture, ignoreCase: true))
                .ThenBy(option => option.Source, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            fontFamilyBox.ItemsSource = families;
            fontFamilyBox.SelectedValue = (families.FirstOrDefault(option =>
                string.Equals(option.Source, activeFontFamily, StringComparison.OrdinalIgnoreCase)) ?? families.FirstOrDefault())?.Source;

            fontSizeBox.Text = settings.FontSize.ToString();
            indentationBox.SelectedItem = settings.IndentWithSpaces ? spacesOption : tabsOption;
            tabWidthBox.Text = settings.TabWidth.ToString();
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
                FontFamily = fontFamilyBox.SelectedValue as string ?? EditorSettings.DefaultFontFamily,
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
