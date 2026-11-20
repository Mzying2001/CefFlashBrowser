using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using Mzying2001.MonkeySharp.Core.Compatibility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CefFlashBrowser.Views.Custom
{
    internal sealed class ScriptEditorCompletion
    {
        private static readonly string[] JavaScriptKeywords =
        {
            "as", "async", "await", "break", "case", "catch", "class", "const", "continue", "debugger", "default",
            "delete", "do", "else", "export", "extends", "false", "finally", "for", "from", "function", "get", "if",
            "import", "in", "instanceof", "let", "new", "null", "of", "return", "set", "static", "super", "switch",
            "this", "throw", "true", "try", "typeof", "var", "void", "while", "with", "yield"
        };

        private static readonly string[] JavaScriptGlobals =
        {
            "Array", "ArrayBuffer", "BigInt", "Boolean", "Date", "Error", "Event", "Infinity", "Intl", "JSON", "Map",
            "Math", "MutationObserver", "NaN", "Number", "Object", "Promise", "Proxy", "Reflect", "RegExp", "Set",
            "String", "Symbol", "URL", "URLSearchParams", "WeakMap", "WeakSet", "XMLHttpRequest", "alert",
            "cancelAnimationFrame", "clearInterval", "clearTimeout", "console", "decodeURIComponent", "document",
            "encodeURIComponent", "fetch", "globalThis", "history", "isFinite", "isNaN", "localStorage", "location",
            "navigator", "parseFloat", "parseInt", "performance", "requestAnimationFrame", "sessionStorage", "setInterval",
            "setTimeout", "undefined", "window", "GM"
        };

        private static readonly UserScriptApiDescriptor[] GmApis = UserScriptGrantCatalog.Apis
            .Where(api => api.Capability.StartsWith("GM.", StringComparison.Ordinal)).ToArray();

        private static readonly CompletionItem[] GlobalItems = CreateItems(JavaScriptKeywords, priority: 1)
            .Concat(CreateItems(JavaScriptGlobals.Concat(GmApis.Select(api => api.LegacyName)))).ToArray();

        private static readonly CompletionItem[] GmMembers = CreateItems(GmApis.Select(api => api.Capability.Substring(3))).ToArray();

        private readonly ScriptEditor _editor;
        private CompletionWindow _window;

        public ScriptEditorCompletion(ScriptEditor editor)
        {
            _editor = editor;

            editor.CommandBindings.Add(new CommandBinding(ScriptEditor.CompleteCodeCommand, (_, __) =>
            {
                editor.TextArea.Focus();
                if (_window == null) Show(manual: true);
            }, (_, e) => e.CanExecute = editor.IsEnabled && !editor.IsReadOnly && editor.TextArea.Selection.IsEmpty));

            editor.InputBindings.Add(new KeyBinding(ScriptEditor.CompleteCodeCommand, Key.Space, ModifierKeys.Control));

            editor.PreviewKeyDown += (_, __) =>
            {
                if (_window != null && _window.CompletionList.SelectedItem == null) Close();
            };

            editor.TextArea.TextEntering += (_, e) =>
            {
                if (!IsIdentifierInput(e.Text)) Close();
            };

            editor.TextArea.TextEntered += TextEntered;

            editor.TextArea.SelectionChanged += (_, __) =>
            {
                if (!editor.TextArea.Selection.IsEmpty) Close();
            };

            editor.IsEnabledChanged += (_, __) =>
            {
                if (!editor.IsEnabled) Close();
            };

            editor.Unloaded += (_, __) => Close();
        }

        public bool Close()
        {
            var window = _window;
            if (window == null) return false;

            _window = null;
            window.Close();
            return true;
        }

        private void TextEntered(object sender, TextCompositionEventArgs e)
        {
            if (!IsIdentifierInput(e.Text)) return;

            if (_window == null)
            {
                Show(manual: false);
            }
            else if (_window.CompletionList.SelectedItem == null)
            {
                Close();
            }
        }

        private void Show(bool manual)
        {
            var document = _editor.Document;
            var textArea = _editor.TextArea;

            if (document == null || !_editor.IsEnabled || _editor.IsReadOnly ||
                !textArea.IsKeyboardFocusWithin || !textArea.Selection.IsEmpty)
                return;

            int caret = textArea.Caret.Offset;
            int start = JavaScriptIdentifier.FindStart(document, caret);

            if ((!manual && caret - start < 2) ||
                ((start < caret || JavaScriptIdentifier.ContinuesAt(document, start)) && !JavaScriptIdentifier.CanStartAt(document, start)))
                return;

            var items = GlobalItems;

            if (start > 0 && document.GetCharAt(start - 1) == '.')
            {
                int receiverEnd = start - 1;
                int receiverStart = JavaScriptIdentifier.FindStart(document, receiverEnd);

                if (receiverEnd - receiverStart != 2 || document.GetText(receiverStart, 2) != "GM" ||
                    (receiverStart > 0 && document.GetCharAt(receiverStart - 1) == '.'))
                    return;

                items = GmMembers;
            }

            string prefix = document.GetText(start, caret - start);

            if (!items.Any(item => item.Text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (manual || item.Text != prefix)) ||
                !IsCodePosition(textArea, document.GetLineByOffset(caret), caret))
                return;

            var window = new CompletionWindow(textArea) { StartOffset = start, Width = 265, CloseWhenCaretAtBeginning = !manual };
            window.Closed += (_, __) => _window = null;
            window.SetResourceReference(Control.BackgroundProperty, "RegionBrush");
            window.CompletionList.SetResourceReference(Control.BackgroundProperty, "RegionBrush");
            window.CompletionList.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");

            foreach (var item in items)
            {
                window.CompletionList.CompletionData.Add(item);
            }

            _window = window;
            window.Show();
            window.CompletionList.SelectItem(prefix);
        }

        private static bool IsCodePosition(TextArea textArea, DocumentLine line, int offset)
        {
            var highlighter = textArea.TextView.GetService(typeof(IHighlighter)) as IHighlighter;

            if (highlighter == null)
                return true;

            if (offset == line.Offset)
                return !highlighter.GetColorStack(line.LineNumber - 1).Any(IsNonCode);

            return !highlighter.HighlightLine(line.LineNumber).Sections.Any(section =>
                section.Offset < offset && offset <= section.Offset + section.Length && IsNonCode(section.Color));
        }

        private static bool IsNonCode(HighlightingColor color) =>
            color.Name == "Comment" || color.Name == "String" || color.Name == "Character" || color.Name == "Regex";

        private static bool IsIdentifierInput(string text) => text.Length == 1 && JavaScriptIdentifier.IsPart(text[0]);

        private static IEnumerable<CompletionItem> CreateItems(IEnumerable<string> names, int priority = 0) => names
            .Where(name => !string.IsNullOrEmpty(name)).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new CompletionItem(name, priority));

        private sealed class CompletionItem : ICompletionData
        {
            public CompletionItem(string text, int priority)
            {
                Text = text;
                Priority = priority;
            }

            public ImageSource Image => null;
            public string Text { get; }
            public object Content => Text;
            public object Description => null;
            public double Priority { get; }

            public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
            {
                var document = textArea.Document;
                int start = completionSegment.Offset;
                int length = JavaScriptIdentifier.FindEnd(document, completionSegment.EndOffset) - start;
                var range = new TextSegment { StartOffset = start, Length = length };
                var readOnly = textArea.ReadOnlySectionProvider;

                if (!readOnly.CanInsert(start) || (length > 0 && !readOnly.GetDeletableSegments(range).Any(segment =>
                    segment.Offset == start && segment.Length == length)))
                    return;

                using (document.RunUpdate())
                {
                    if (document.GetText(range) != Text)
                        document.Replace(range, Text);

                    textArea.Caret.Offset = start + Text.Length;
                }

                textArea.Caret.BringCaretToView();
            }
        }
    }
}
