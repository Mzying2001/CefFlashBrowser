using CefFlashBrowser.Models;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml;

namespace CefFlashBrowser.Views.Custom
{
    public class ScriptEditor : TextEditor
    {
        private readonly IHighlightingDefinition _javascript;
        private readonly ScriptEditorCompletion _completion;
        private readonly ScriptEditorOccurrences _occurrences;
        private bool _updatingText;

        internal ScriptEditorSearch Search { get; }

        public static RoutedCommand CompleteCodeCommand { get; } = new RoutedCommand(nameof(CompleteCodeCommand), typeof(ScriptEditor));

        public static readonly DependencyProperty SourceTextProperty = DependencyProperty.Register(
            nameof(SourceText), typeof(string), typeof(ScriptEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSourceTextChanged));

        private static void OnSourceTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var editor = (ScriptEditor)d;
            editor.UpdateText((string)e.NewValue);
        }

        public string SourceText
        {
            get => (string)GetValue(SourceTextProperty);
            set => SetValue(SourceTextProperty, value);
        }


        public ScriptEditor()
        {
            // Load a private definition: recoloring the shared HighlightingManager instance
            // would also change other editors using JavaScript highlighting.
            using (var stream = typeof(TextEditor).Assembly.GetManifestResourceStream(
                "ICSharpCode.AvalonEdit.Highlighting.Resources.JavaScript-Mode.xshd"))
            using (var reader = XmlReader.Create(stream))
            {
                _javascript = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }

            Search = new ScriptEditorSearch(this);
            _completion = new ScriptEditorCompletion(this);
            _occurrences = new ScriptEditorOccurrences(this);
            SyntaxHighlighting = _javascript;
            ApplySettings(new EditorSettings());
            SetResourceReference(BackgroundProperty, "RegionBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            SetResourceReference(BorderBrushProperty, "BorderBrush");
            TextArea.SetResourceReference(TextArea.SelectionForegroundProperty, "PrimaryTextBrush");
            TextArea.SelectionBorder = null;
            UpdatePalette();
        }

        internal void ApplySettings(EditorSettings settings)
        {
            var familyName = settings.FontFamily?.Trim();

            var font = Fonts.SystemFontFamilies.FirstOrDefault(family =>
                string.Equals(family.Source, familyName, StringComparison.OrdinalIgnoreCase));

            FontFamily = font ?? new FontFamily(EditorSettings.DefaultFontFamily);
            FontSize = settings.FontSize;
            Options.ConvertTabsToSpaces = settings.IndentWithSpaces;
            Options.IndentationSize = settings.TabWidth;
            ShowLineNumbers = settings.ShowLineNumbers;
            WordWrap = settings.WordWrap;
        }

        internal bool CloseCompletion() => _completion.Close();

        internal void SetSearchOpen(bool isOpen) => _occurrences.IsSuppressed = isOpen;

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);

            if (e.Handled || Keyboard.Modifiers != ModifierKeys.Shift || e.Delta == 0)
                return;

            e.Handled = true;

            var textView = TextArea.TextView;
            IScrollInfo scrollInfo = textView;

            int lines = SystemParameters.WheelScrollLines;
            double step = lines < 0 ? scrollInfo.ViewportWidth : lines * textView.WideSpaceWidth;
            double offset = scrollInfo.HorizontalOffset - step * e.Delta / Mouse.MouseWheelDeltaForOneLine;
            double maxOffset = Math.Max(0, scrollInfo.ExtentWidth - scrollInfo.ViewportWidth);

            scrollInfo.SetHorizontalOffset(Math.Max(0, Math.Min(offset, maxOffset)));
            scrollInfo.ScrollOwner?.InvalidateScrollInfo();
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);

            if (!_updatingText && SourceText != Text)
            {
                // Preserve the binding, and do not reload the document when its source echoes back.
                SetCurrentValue(SourceTextProperty, Text);
            }
        }

        internal void SetSourceDocument(TextDocument document, BindingBase sourceBinding)
        {
            CloseCompletion();
            // Changing the document raises TextChanged. Keep that notification out of the old draft.
            _updatingText = true;

            try
            {
                BindingOperations.ClearBinding(this, SourceTextProperty);
                Document = document;
                SetCurrentValue(SourceTextProperty, document.Text);
            }
            finally
            {
                _updatingText = false;
            }

            if (sourceBinding != null)
            {
                SetBinding(SourceTextProperty, sourceBinding);
            }
        }

        private void UpdateText(string text)
        {
            text = text ?? string.Empty;
            if (_updatingText || Text == text) return;

            CloseCompletion();
            _updatingText = true;

            try
            {
                Text = text;
            }
            finally
            {
                _updatingText = false;
            }
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // Observe the actual resource colors, including system-theme changes and
            // windows created after the application's theme was selected.
            if (e.Property == BackgroundProperty && _javascript != null)
            {
                UpdatePalette();
            }
            else if (e.Property == ForegroundProperty && TextArea != null)
            {
                TextArea.Caret.CaretBrush = Foreground;
            }
            else if (e.Property == IsReadOnlyProperty && IsReadOnly)
            {
                _completion?.Close();
            }
        }

        private void UpdatePalette()
        {
            var background = (Background as SolidColorBrush)?.Color ?? Colors.White;
            bool dark = background.R * 0.299 + background.G * 0.587 + background.B * 0.114 < 128;

            SetHighlightingColor("Digits", dark ? "#B5CEA8" : "#098658");
            SetHighlightingColor("Comment", dark ? "#6A9955" : "#008000");
            SetHighlightingColor("String", dark ? "#CE9178" : "#A31515");
            SetHighlightingColor("Character", dark ? "#CE9178" : "#A31515");
            SetHighlightingColor("Regex", dark ? "#D16969" : "#811F3F");
            SetHighlightingColor("JavaScriptKeyWords", dark ? "#569CD6" : "#0000FF");
            SetHighlightingColor("JavaScriptIntrinsics", dark ? "#4EC9B0" : "#267F99");
            SetHighlightingColor("JavaScriptLiterals", dark ? "#569CD6" : "#0000FF");
            SetHighlightingColor("JavaScriptGlobalFunctions", dark ? "#DCDCAA" : "#795E26");
            LineNumbersForeground = CreateBrush(dark ? "#9D9D9D" : "#757575");
            TextArea.SelectionBrush = CreateBrush(dark ? "#264F78" : "#ADD6FF");
            Search.MarkerBrush = CreateBrush(dark ? "#453919" : "#FFF0AE");
            _occurrences.MarkerBrush = CreateBrush(dark ? "#33434B" : "#DFE9ED");
            TextArea.TextView.LinkTextForegroundBrush = CreateBrush(dark ? "#4DAAFC" : "#006AB1");
            TextArea.TextView.Redraw();
        }

        private void SetHighlightingColor(string name, string color)
        {
            _javascript.GetNamedColor(name).Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private static SolidColorBrush CreateBrush(string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            return brush;
        }
    }
}
