using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace CefFlashBrowser.Views.Custom
{
    internal sealed class ScriptEditorSearch : IBackgroundRenderer
    {
        private readonly ScriptEditor _editor;
        private readonly List<int> _offsets = new List<int>();
        private string _query = string.Empty;
        private StringComparison _comparison;

        public ScriptEditorSearch(ScriptEditor editor)
        {
            _editor = editor;
            _editor.TextArea.TextView.BackgroundRenderers.Add(this);

            _editor.TextChanged += (_, __) =>
            {
                Refresh();
            };

            _editor.TextArea.SelectionChanged += (_, __) =>
            {
                if (Count > 0) _editor.TextArea.TextView.InvalidateLayer(Layer);
            };
        }

        public KnownLayer Layer => KnownLayer.Selection;
        public Brush MarkerBrush { get; set; }
        public int Count => _offsets.Count;
        public bool HasCurrentMatch => SelectedMatchIndex >= 0;

        private int SelectedMatchIndex =>
            Count > 0 && !(_editor.TextArea.Selection is RectangleSelection) && _editor.SelectionLength == _query.Length
                ? _offsets.BinarySearch(_editor.SelectionStart) : -1;

        public void SetQuery(string query, bool matchCase)
        {
            _query = query;
            _comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            Refresh();
        }

        private void Refresh()
        {
            if (_query.Length == 0 && Count == 0)
                return;

            _offsets.Clear();

            if (_query.Length != 0)
            {
                string text = _editor.Text;
                int offset = 0;

                while ((offset = text.IndexOf(_query, offset, _comparison)) >= 0)
                {
                    _offsets.Add(offset);
                    offset += _query.Length;
                }
            }

            _editor.TextArea.TextView.InvalidateLayer(Layer);
        }

        public void Find(bool forward, bool includeCurrent = false)
        {
            if (Count == 0)
                return;

            int index = SelectedMatchIndex;

            if (index >= 0)
            {
                if (!includeCurrent)
                    index += forward ? 1 : -1;
            }
            else
            {
                index = LowerBound(_editor.CaretOffset);
                if (!forward) index--;
            }

            if (index >= Count) index = 0;
            if (index < 0) index = Count - 1;
            SelectMatch(index);
        }

        public void ReplaceCurrent(string replacement)
        {
            int index = SelectedMatchIndex;
            if (index < 0) return;

            int offset = _offsets[index];

            if (_editor.Document.GetText(offset, _query.Length) == replacement)
            {
                Find(forward: true);
                return;
            }

            _editor.Document.Replace(offset, _query.Length, replacement);

            int nextOffset = offset + replacement.Length;
            int nextIndex = LowerBound(nextOffset);

            if (nextIndex < Count)
            {
                SelectMatch(nextIndex);
            }
            else if (Count > 0 && _offsets[0] + _query.Length <= offset)
            {
                SelectMatch(0);
            }
            else
            {
                _editor.CaretOffset = nextOffset;
                _editor.TextArea.ClearSelection();
            }
        }

        public void ReplaceAll(string replacement)
        {
            if (Count == 0) return;

            var document = _editor.Document;
            using (document.RunUpdate())
            {
                for (int index = Count - 1; index >= 0; index--)
                {
                    int offset = _offsets[index];
                    if (document.GetText(offset, _query.Length) != replacement)
                        document.Replace(offset, _query.Length, replacement);
                }
            }
        }

        public void Draw(TextView view, DrawingContext drawingContext)
        {
            if (Count == 0 || !view.VisualLinesValid || view.VisualLines.Count == 0)
                return;

            var lines = view.VisualLines;
            int firstOffset = lines[0].FirstDocumentLine.Offset;
            var lastLine = lines[lines.Count - 1].LastDocumentLine;
            int endOffset = lastLine.Offset + lastLine.TotalLength;
            int selectedMatch = SelectedMatchIndex;
            var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true };

            for (int index = LowerBound(firstOffset - _query.Length + 1);
                index < Count && _offsets[index] < endOffset; index++)
            {
                if (index != selectedMatch)
                    builder.AddSegment(view, new TextSegment { StartOffset = _offsets[index], Length = _query.Length });
            }

            var geometry = builder.CreateGeometry();

            if (geometry != null)
                drawingContext.DrawGeometry(MarkerBrush, null, geometry);
        }

        private int LowerBound(int offset)
        {
            int index = _offsets.BinarySearch(offset);
            return index >= 0 ? index : ~index;
        }

        private void SelectMatch(int index)
        {
            int offset = _offsets[index];
            _editor.CaretOffset = offset;
            _editor.Select(offset, _query.Length);
            _editor.TextArea.Caret.BringCaretToView();
        }
    }
}
