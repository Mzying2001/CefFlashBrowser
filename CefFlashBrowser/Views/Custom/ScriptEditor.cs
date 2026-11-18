using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using System;
using System.Windows;
using System.Windows.Media;
using System.Xml;

namespace CefFlashBrowser.Views.Custom
{
    public class ScriptEditor : TextEditor
    {
        public static readonly DependencyProperty SourceTextProperty = DependencyProperty.Register(
            nameof(SourceText), typeof(string), typeof(ScriptEditor),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                (d, e) => ((ScriptEditor)d).UpdateText((string)e.NewValue)));

        private readonly IHighlightingDefinition _javascript;
        private bool _updatingText;

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

            SyntaxHighlighting = _javascript;
            Options.IndentationSize = 2;
            Options.ConvertTabsToSpaces = true;
            SetResourceReference(BackgroundProperty, "RegionBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            SetResourceReference(BorderBrushProperty, "BorderBrush");
            TextArea.SetResourceReference(ICSharpCode.AvalonEdit.Editing.TextArea.SelectionForegroundProperty, "PrimaryTextBrush");
            TextArea.SelectionBorder = null;
            UpdatePalette();
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

        private void UpdateText(string text)
        {
            text = text ?? string.Empty;
            if (Text == text) return;

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
