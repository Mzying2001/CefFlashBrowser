using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Windows;
using System.Windows.Media;

namespace CefFlashBrowser.Views.Custom
{
    internal sealed class ScriptEditorOccurrences : IBackgroundRenderer
    {
        private readonly ScriptEditor _editor;
        private bool _isSuppressed;

        public ScriptEditorOccurrences(ScriptEditor editor)
        {
            _editor = editor;
            var textView = editor.TextArea.TextView;
            textView.BackgroundRenderers.Add(this);
            editor.TextArea.SelectionChanged += (_, __) => textView.InvalidateLayer(Layer);
        }

        public KnownLayer Layer => KnownLayer.Selection;
        public Brush MarkerBrush { get; set; }

        public bool IsSuppressed
        {
            get => _isSuppressed;
            set
            {
                if (_isSuppressed == value) return;
                _isSuppressed = value;
                _editor.TextArea.TextView.InvalidateLayer(Layer);
            }
        }

        public void Draw(TextView view, DrawingContext drawingContext)
        {
            if (IsSuppressed || !view.VisualLinesValid || view.VisualLines.Count == 0 ||
                !(view.Document is TextDocument document))
                return;

            var selection = _editor.TextArea.Selection;

            if (selection.IsEmpty || selection is RectangleSelection || selection.IsMultiline)
                return;

            var selectedRange = selection.SurroundingSegment;

            if (selectedRange.Length == 0 || !JavaScriptIdentifier.CanStartAt(document, selectedRange.Offset) ||
                JavaScriptIdentifier.ContinuesBefore(document, selectedRange.Offset) ||
                JavaScriptIdentifier.FindEnd(document, selectedRange.Offset) != selectedRange.EndOffset)
                return;

            string word = document.GetText(selectedRange);
            var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 2 };

            foreach (var line in view.VisualLines)
            {
                int start = line.FirstDocumentLine.Offset;
                int end = line.LastDocumentLine.EndOffset;

                if (line.TextLines.Count == 1)
                {
                    // Include identifiers crossing either edge of the horizontal viewport.
                    double left = view.ScrollOffset.X;
                    int first = line.GetRelativeOffset(line.GetVisualColumnFloor(new Point(left, line.VisualTop), false));
                    int last = line.GetRelativeOffset(line.GetVisualColumnFloor(new Point(left + view.ActualWidth, line.VisualTop), false));
                    int lineStart = start;
                    start = lineStart + Math.Max(0, Math.Min(first, last) - word.Length + 1);
                    end = (int)Math.Min(end, (long)lineStart + Math.Max(first, last) + word.Length);
                }

                string text = document.GetText(start, end - start);

                for (int index = text.IndexOf(word, StringComparison.Ordinal); index >= 0;
                    index = text.IndexOf(word, index + word.Length, StringComparison.Ordinal))
                {
                    int offset = start + index;
                    if (offset != selectedRange.Offset && !JavaScriptIdentifier.ContinuesBefore(document, offset) &&
                        !JavaScriptIdentifier.ContinuesAt(document, offset + word.Length))
                        builder.AddSegment(view, new TextSegment { StartOffset = offset, Length = word.Length });
                }
            }

            var geometry = builder.CreateGeometry();

            if (geometry != null)
                drawingContext.DrawGeometry(MarkerBrush, null, geometry);
        }
    }
}
