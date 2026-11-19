using CefFlashBrowser.ViewModels;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CefFlashBrowser.Views
{
    public partial class UserScriptManager
    {
        private const double ScriptScrollEdge = 24;

        private UserScriptItemViewModel _scriptDragCandidate;
        private Point _scriptDragStart;
        private UserScriptItemViewModel _draggedScript;
        private ScrollContentPresenter _scriptDragViewport;
        private Point _scriptDragPosition;
        private DispatcherTimer _scriptDragTimer;
        private AdornerLayer _scriptInsertLayer;
        private ScriptInsertAdorner _scriptInsertLine;

        private void ScriptListPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _draggedScript != null)
                return;

            _scriptDragCandidate = null;

            var source = e.OriginalSource as DependencyObject;
            var row = source == null ? null : ItemsControl.ContainerFromElement(scriptList, source) as ListBoxItem;

            if (row == null || !(scriptList.ItemContainerGenerator.ItemFromContainer(row) is UserScriptItemViewModel item)
                || !ViewModel.CanDragScript(item))
                return;

            // The enable switch owns its click and mouse capture.
            for (var element = source; element != null && element != row;)
            {
                if (element is ButtonBase)
                    return;

                element = element is Visual ? VisualTreeHelper.GetParent(element)
                    : element is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(element);
            }

            _scriptDragCandidate = item;
            _scriptDragStart = e.GetPosition(scriptList);
        }

        private void ScriptListPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                _scriptDragCandidate = null;
        }

        private void ScriptListMouseLeave(object sender, MouseEventArgs e) => _scriptDragCandidate = null;

        private void ScriptListPreviewMouseMove(object sender, MouseEventArgs e)
        {
            var item = _scriptDragCandidate;
            if (item == null) return;

            if (e.LeftButton != MouseButtonState.Pressed || !ViewModel.CanDragScript(item))
            {
                _scriptDragCandidate = null;
                return;
            }

            var distance = e.GetPosition(scriptList) - _scriptDragStart;

            if (Math.Abs(distance.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(distance.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _scriptDragCandidate = null;

            if (!BeginScriptDrag(item))
                return;

            e.Handled = true;

            try
            {
                DragDrop.DoDragDrop(scriptList, new DataObject(typeof(UserScriptItemViewModel), item), DragDropEffects.Move);
            }
            finally
            {
                EndScriptDrag();
            }
        }

        private bool BeginScriptDrag(UserScriptItemViewModel item)
        {
            _scriptDragViewport = FindScrollContentPresenter(scriptList);
            if (_scriptDragViewport == null)
                return false;

            _draggedScript = item;
            return true;
        }

        private static ScrollContentPresenter FindScrollContentPresenter(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var presenter = child as ScrollContentPresenter ?? FindScrollContentPresenter(child);

                if (presenter != null)
                    return presenter;
            }

            return null;
        }

        private bool IsScriptDrag(DragEventArgs e)
            => _draggedScript != null && (e.AllowedEffects & DragDropEffects.Move) != 0
                && ReferenceEquals(e.Data.GetData(typeof(UserScriptItemViewModel)), _draggedScript);

        private void ScriptListDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.None;

            if (IsScriptDrag(e) && UpdateScriptDrag(e.GetPosition(scriptList)))
                e.Effects = DragDropEffects.Move;
        }

        private void ScriptListDragLeave(object sender, DragEventArgs e) => ClearScriptDropFeedback();

        private async void ScriptListDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            e.Effects = DragDropEffects.None;

            ClearScriptDropFeedback();

            if (IsScriptDrag(e) && TryGetScriptViewportPoint(e.GetPosition(scriptList), out var point)
                && TryGetScriptDrop(point, out var target, out var after, out _))
            {
                e.Effects = DragDropEffects.Move;
                await ViewModel.MoveRelativeAsync(_draggedScript, target, after);
            }
        }

        private void ScriptListQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (_draggedScript != null && (e.EscapePressed || !scriptList.IsVisible || !ViewModel.CanDragScript(_draggedScript)))
            {
                e.Action = DragAction.Cancel;
                e.Handled = true;
                ClearScriptDropFeedback();
            }
        }

        private bool TryGetScriptViewportPoint(Point position, out Point point)
        {
            point = default(Point);

            if (_scriptDragViewport == null || !scriptList.IsVisible || !ViewModel.CanDragScript(_draggedScript))
                return false;

            point = scriptList.TranslatePoint(position, _scriptDragViewport);

            return point.X >= 0 && point.X < _scriptDragViewport.ActualWidth
                && point.Y >= 0 && point.Y < _scriptDragViewport.ActualHeight;
        }

        private bool TryGetScriptDrop(Point point, out UserScriptItemViewModel target, out bool after, out double lineY)
        {
            target = null;
            after = false;
            lineY = 0;

            var hit = _scriptDragViewport.InputHitTest(point) as DependencyObject;
            var row = hit == null ? null : ItemsControl.ContainerFromElement(scriptList, hit) as ListBoxItem;

            if (row == null && scriptList.Items.Count > 0
                && scriptList.Items[scriptList.Items.Count - 1] is UserScriptItemViewModel last && last.IsInstalled)
            {
                row = scriptList.ItemContainerGenerator.ContainerFromItem(last) as ListBoxItem;
                if (row != null && point.Y < row.TranslatePoint(new Point(0, row.ActualHeight), _scriptDragViewport).Y)
                    row = null;
            }

            if (row == null)
                return false;

            target = scriptList.ItemContainerGenerator.ItemFromContainer(row) as UserScriptItemViewModel;
            var top = row.TranslatePoint(new Point(), _scriptDragViewport).Y;
            after = point.Y >= top + row.ActualHeight / 2;
            lineY = after ? top + row.ActualHeight : top;
            return ViewModel.CanMoveRelative(_draggedScript, target, after);
        }

        private bool UpdateScriptDrag(Point position)
        {
            if (!TryGetScriptViewportPoint(position, out var point))
            {
                ClearScriptDropFeedback();
                return false;
            }

            _scriptDragPosition = position;

            if (GetScriptScrollDirection(point) != 0)
            {
                if (_scriptDragTimer == null)
                {
                    _scriptDragTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(150)
                    };
                    _scriptDragTimer.Tick += ScrollDuringScriptDrag;
                }

                _scriptDragTimer.Start();
            }
            else
            {
                _scriptDragTimer?.Stop();
            }

            var canDrop = TryGetScriptDrop(point, out _, out _, out var lineY);

            if (canDrop)
                ShowScriptInsertLine(lineY);
            else
                HideScriptInsertLine();

            return canDrop;
        }

        private int GetScriptScrollDirection(Point point)
        {
            if (!(_scriptDragViewport.TemplatedParent is ScrollViewer scroll))
                return 0;

            var edge = Math.Min(ScriptScrollEdge, _scriptDragViewport.ActualHeight / 2);

            if (point.Y < edge && scroll.VerticalOffset > 0)
                return -1;

            if (point.Y > _scriptDragViewport.ActualHeight - edge && scroll.VerticalOffset < scroll.ScrollableHeight)
            {
                var last = ViewModel.Scripts.TakeWhile(item => item.IsInstalled).LastOrDefault();
                var row = scriptList.ItemContainerGenerator.ContainerFromItem(last) as ListBoxItem;

                if (row == null || row.TranslatePoint(new Point(0, row.ActualHeight), _scriptDragViewport).Y > _scriptDragViewport.ActualHeight)
                    return 1;
            }

            return 0;
        }

        private void ScrollDuringScriptDrag(object sender, EventArgs e)
        {
            if (TryGetScriptViewportPoint(_scriptDragPosition, out var point))
            {
                var direction = GetScriptScrollDirection(point);

                if (direction != 0 && _scriptDragViewport.TemplatedParent is ScrollViewer scroll)
                {
                    if (direction < 0)
                        scroll.LineUp();
                    else
                        scroll.LineDown();

                    scroll.UpdateLayout();
                }
            }

            UpdateScriptDrag(_scriptDragPosition);
        }

        private void ShowScriptInsertLine(double y)
        {
            if (_scriptInsertLine == null)
            {
                _scriptInsertLayer = AdornerLayer.GetAdornerLayer(_scriptDragViewport);

                if (_scriptInsertLayer == null)
                    return;

                _scriptInsertLine = new ScriptInsertAdorner(_scriptDragViewport);
                _scriptInsertLayer.Add(_scriptInsertLine);
            }

            _scriptInsertLine.SetPosition(y);
        }

        private void HideScriptInsertLine()
        {
            if (_scriptInsertLine == null)
                return;

            _scriptInsertLayer.Remove(_scriptInsertLine);
            _scriptInsertLayer = null;
            _scriptInsertLine = null;
        }

        private void ClearScriptDropFeedback()
        {
            _scriptDragTimer?.Stop();
            HideScriptInsertLine();
        }

        private void EndScriptDrag()
        {
            ClearScriptDropFeedback();
            _scriptDragCandidate = null;
            _draggedScript = null;
            _scriptDragViewport = null;
        }

        private sealed class ScriptInsertAdorner : Adorner
        {
            private double _y;

            public ScriptInsertAdorner(ScrollContentPresenter viewport) : base(viewport)
            {
                IsHitTestVisible = false;
            }

            public void SetPosition(double y)
            {
                _y = y;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                var viewport = (ScrollContentPresenter)AdornedElement;
                var y = Math.Max(1, Math.Min(_y, viewport.ActualHeight - 1));
                var brush = viewport.TryFindResource("PrimaryBrush") as Brush ?? SystemColors.HighlightBrush;
                drawingContext.DrawLine(new Pen(brush, 2), new Point(2, y), new Point(viewport.ActualWidth - 2, y));
            }
        }
    }
}
