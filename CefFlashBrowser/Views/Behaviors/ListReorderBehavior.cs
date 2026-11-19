using CefFlashBrowser.Infrastructure.Collections;
using CefFlashBrowser.Utils;
using Microsoft.Xaml.Behaviors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CefFlashBrowser.Views.Behaviors
{
    /// <summary>Requests item moves in a vertical list with AllowDrop enabled.</summary>
    public sealed class ListReorderBehavior : Behavior<ListBox>
    {
        public static readonly DependencyProperty IsDragEnabledProperty = DependencyProperty.Register(
            nameof(IsDragEnabled), typeof(bool), typeof(ListReorderBehavior), new PropertyMetadata(false, OnIsDragEnabledChanged));

        public static readonly DependencyProperty ReorderCommandProperty = DependencyProperty.Register(
            nameof(ReorderCommand), typeof(ICommand), typeof(ListReorderBehavior));

        public static readonly DependencyProperty CanReorderItemProperty = DependencyProperty.Register(
            nameof(CanReorderItem), typeof(Predicate<object>), typeof(ListReorderBehavior));

        private const double ScrollEdge = 24;
        private const double PreviewBorderThickness = 1;
        private object _dragCandidate;
        private Point _dragStart;
        private Vector _previewOffset;
        private DragSession _drag;
        private Point _dragPosition;
        private DispatcherTimer _scrollTimer;
        private AdornerLayer _insertLayer;
        private InsertAdorner _insertLine;
        private Popup _dragPreview;
        private bool _usedPreviewCursor;

        public bool IsDragEnabled
        {
            get => (bool)GetValue(IsDragEnabledProperty);
            set => SetValue(IsDragEnabledProperty, value);
        }

        /// <summary>Handles a ListReorderRequest and owns any collection changes.</summary>
        public ICommand ReorderCommand
        {
            get => (ICommand)GetValue(ReorderCommandProperty);
            set => SetValue(ReorderCommandProperty, value);
        }

        /// <summary>Optionally restricts which items can be dragged or used as a target.</summary>
        public Predicate<object> CanReorderItem
        {
            get => (Predicate<object>)GetValue(CanReorderItemProperty);
            set => SetValue(CanReorderItemProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();
            var list = AssociatedObject;
            list.PreviewMouseDown += OnPreviewMouseDown;
            list.PreviewMouseUp += OnPreviewMouseUp;
            list.PreviewMouseMove += OnPreviewMouseMove;
            list.MouseLeave += OnMouseLeave;
            list.PreviewDragEnter += OnDragOver;
            list.PreviewDragOver += OnDragOver;
            list.PreviewDragLeave += OnDragLeave;
            list.PreviewDrop += OnDrop;
            list.Unloaded += OnUnloaded;
            list.IsEnabledChanged += OnAvailabilityChanged;
            list.IsVisibleChanged += OnAvailabilityChanged;
        }

        protected override void OnDetaching()
        {
            CancelDrag();
            var list = AssociatedObject;
            list.PreviewMouseDown -= OnPreviewMouseDown;
            list.PreviewMouseUp -= OnPreviewMouseUp;
            list.PreviewMouseMove -= OnPreviewMouseMove;
            list.MouseLeave -= OnMouseLeave;
            list.PreviewDragEnter -= OnDragOver;
            list.PreviewDragOver -= OnDragOver;
            list.PreviewDragLeave -= OnDragLeave;
            list.PreviewDrop -= OnDrop;
            list.Unloaded -= OnUnloaded;
            list.IsEnabledChanged -= OnAvailabilityChanged;
            list.IsVisibleChanged -= OnAvailabilityChanged;
            base.OnDetaching();
        }

        private static void OnIsDragEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
                ((ListReorderBehavior)d).CancelDrag();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => CancelDrag();

        private void OnAvailabilityChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
                CancelDrag();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || _drag != null)
                return;

            _dragCandidate = null;
            var list = AssociatedObject;

            if (!IsDragEnabled || ReorderCommand == null)
                return;

            var source = e.OriginalSource as DependencyObject;
            var row = GetItemContainer(list, source);

            if (row == null)
                return;

            var index = list.ItemContainerGenerator.IndexFromContainer(row);

            if (index < 0 || !CanStartDrag(list, list.Items[index]))
                return;

            // Buttons retain their click and mouse capture.
            for (var element = source; element != null && element != row;)
            {
                if (element is ButtonBase)
                    return;

                element = element is Visual ? VisualTreeHelper.GetParent(element)
                    : element is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(element);
            }

            _dragCandidate = list.Items[index];
            _dragStart = e.GetPosition(list);
            _previewOffset = (Vector)e.GetPosition(row) + new Vector(PreviewBorderThickness, PreviewBorderThickness);
        }

        private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                _dragCandidate = null;
        }

        private void OnMouseLeave(object sender, MouseEventArgs e) => _dragCandidate = null;

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            var item = _dragCandidate;
            if (item == null) return;

            var list = AssociatedObject;

            if (e.LeftButton != MouseButtonState.Pressed || !CanStartDrag(list, item))
            {
                _dragCandidate = null;
                return;
            }

            var position = e.GetPosition(list);
            var distance = position - _dragStart;

            if (Math.Abs(distance.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(distance.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _dragCandidate = null;

            var data = BeginDrag(item);
            if (data == null) return;

            e.Handled = true;

            try
            {
                ShowDragPreview(position);
                DragDrop.DoDragDrop(list, data, DragDropEffects.Move);
            }
            finally
            {
                EndDrag();
            }
        }

        private DataObject BeginDrag(object item)
        {
            var list = AssociatedObject;

            if (_drag != null || !CanStartDrag(list, item))
                return null;

            var viewport = FindItemViewport(list, item);

            if (viewport == null)
                return null;

            _drag = new DragSession(list, viewport, item);
            // Keep the source handlers until OLE exits, even if the behavior is detached.
            list.PreviewQueryContinueDrag += OnQueryContinueDrag;
            list.PreviewGiveFeedback += OnGiveFeedback;
            return new DataObject(typeof(DragToken), _drag.Token);
        }

        private void ShowDragPreview(Point position)
        {
            var list = _drag.List;

            if (!(list.ItemContainerGenerator.ContainerFromItem(_drag.Item) is ListBoxItem row)
                || row.ActualWidth <= 0 || row.ActualHeight <= 0 || VisualTreeHelper.GetChildrenCount(row) == 0)
                return;

            var size = row.RenderSize;
            var dpi = VisualTreeHelper.GetDpi(row);

            var snapshot = new RenderTargetBitmap(
                (int)Math.Ceiling(size.Width * dpi.DpiScaleX), (int)Math.Ceiling(size.Height * dpi.DpiScaleY),
                dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);

            // Render the template root to exclude the container's offset within the list.
            snapshot.Render((Visual)VisualTreeHelper.GetChild(row, 0));
            snapshot.Freeze();

            var preview = new Border
            {
                Opacity = 0.6,
                BorderThickness = new Thickness(PreviewBorderThickness),
                BorderBrush = list.TryFindResource("BorderBrush") as Brush ?? SystemColors.ActiveBorderBrush,
                Background = list.TryFindResource("RegionBrush") as Brush ?? SystemColors.WindowBrush,
                CornerRadius = new CornerRadius(6),
                Child = new Image { Source = snapshot, Width = size.Width, Height = size.Height, Stretch = Stretch.Fill }
            };

            _dragPreview = new Popup
            {
                Placement = PlacementMode.RelativePoint,
                PlacementTarget = list,
                HorizontalOffset = position.X - _previewOffset.X,
                VerticalOffset = position.Y - _previewOffset.Y,
                AllowsTransparency = true,
                StaysOpen = true,
                Focusable = false,
                IsHitTestVisible = false,
                Child = preview
            };
            _dragPreview.Opened += OnDragPreviewOpened;
            _dragPreview.IsOpen = true;
        }

        private void OnDragPreviewOpened(object sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(_dragPreview.Child) is HwndSource source)
            {
                // Let OLE hit-test the list beneath the native popup.
                HwndHelper.SetWindowExStyle(source.Handle, HwndHelper.GetWindowExStyle(source.Handle)
                    | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE);
                PositionDragPreview(source);
            }
        }

        private void PositionDragPreview(HwndSource source)
        {
            if (!Win32.GetCursorPos(out var cursor))
                return;

            var offset = source.CompositionTarget.TransformToDevice.Transform(_previewOffset);

            Win32.SetWindowPos(source.Handle, IntPtr.Zero, cursor.x - (int)Math.Round(offset.X),
                cursor.y - (int)Math.Round(offset.Y), 0, 0,
                Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
        }

        private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (_dragPreview?.IsOpen != true || !(PresentationSource.FromVisual(_dragPreview.Child) is HwndSource source))
                return;

            PositionDragPreview(source);

            if (e.Effects == DragDropEffects.Move)
            {
                Mouse.SetCursor(Cursors.Arrow);
                _usedPreviewCursor = true;
                e.UseDefaultCursors = false;
                e.Handled = true;
            }
        }

        private void HideDragPreview()
        {
            if (_dragPreview != null)
            {
                _dragPreview.Opened -= OnDragPreviewOpened;
                _dragPreview.IsOpen = false;
                _dragPreview.Child = null;
                _dragPreview.PlacementTarget = null;
                _dragPreview = null;
            }

            if (_usedPreviewCursor)
            {
                Mouse.SetCursor(null);
                _usedPreviewCursor = false;
            }
        }

        private bool CanStartDrag(ListBox list, object item)
            => IsDragEnabled && ReorderCommand != null && list != null && list.AllowDrop
                && list.IsLoaded && list.IsVisible && list.IsEnabled && list.Items.Count > 1
                && IsItemEligible(item) && list.Items.Contains(item);

        private bool IsItemEligible(object item)
            => item != null && (CanReorderItem == null || CanReorderItem(item));

        private bool CanContinueDrag()
            => _drag != null && !_drag.Cancelled && ReferenceEquals(_drag.List, AssociatedObject)
                && CanStartDrag(_drag.List, _drag.Item) && _drag.Viewport.IsDescendantOf(_drag.List);

        private static ListBoxItem GetItemContainer(ListBox list, DependencyObject source)
            => source == null ? null : ItemsControl.ContainerFromElement(list, source) as ListBoxItem;

        private static ScrollContentPresenter FindItemViewport(ListBox list, object item)
        {
            // A GridView can have another scroll presenter for its column headers.
            for (var element = list.ItemContainerGenerator.ContainerFromItem(item);
                element != null && element != list; element = VisualTreeHelper.GetParent(element))
            {
                if (element is ScrollContentPresenter presenter)
                    return presenter;
            }

            return null;
        }

        private bool IsOwnDrag(DragEventArgs e)
            => _drag != null && ReferenceEquals(e.Data.GetData(typeof(DragToken)), _drag.Token);

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (!IsDragEnabled || !IsOwnDrag(e))
                return;

            e.Handled = true;
            e.Effects = DragDropEffects.None;

            if ((e.AllowedEffects & DragDropEffects.Move) == 0)
            {
                ClearFeedback();
            }
            else if (UpdateDrag(e.GetPosition(_drag.List)))
            {
                e.Effects = DragDropEffects.Move;
            }
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            if (IsOwnDrag(e))
                ClearFeedback();
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (!IsDragEnabled || !IsOwnDrag(e))
                return;

            e.Handled = true;
            e.Effects = DragDropEffects.None;
            ClearFeedback();

            var command = ReorderCommand;

            if ((e.AllowedEffects & DragDropEffects.Move) == 0
                || !TryGetViewportPoint(e.GetPosition(_drag.List), out var point)
                || !TryGetDrop(point, command, out var request, out _) || !CanContinueDrag())
                return;

            // Consume the session before invoking user code, which can change the list or detach the behavior.
            CancelDrag();
            e.Effects = DragDropEffects.Move;
            command.Execute(request);
        }

        private void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed || !CanContinueDrag())
            {
                e.Action = DragAction.Cancel;
                e.Handled = true;
                CancelDrag();
            }
        }

        private bool TryGetViewportPoint(Point position, out Point point)
        {
            point = default(Point);

            if (!CanContinueDrag())
                return false;

            var viewport = _drag.Viewport;
            point = _drag.List.TranslatePoint(position, viewport);
            return point.X >= 0 && point.X < viewport.ActualWidth && point.Y >= 0 && point.Y < viewport.ActualHeight;
        }

        private bool TryGetDrop(Point point, ICommand command, out ListReorderRequest request, out double lineY)
        {
            request = null;
            lineY = 0;

            var list = _drag.List;
            var viewport = _drag.Viewport;
            var row = GetItemContainer(list, viewport.InputHitTest(point) as DependencyObject);

            if (row == null)
            {
                // Empty space below the final row is another way to drop after that row.
                row = list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count - 1) as ListBoxItem;

                if (row == null || point.Y < row.TranslatePoint(new Point(0, row.ActualHeight), viewport).Y)
                    return false;
            }

            var targetIndex = list.ItemContainerGenerator.IndexFromContainer(row);

            if (targetIndex < 0 || !IsItemEligible(list.Items[targetIndex]))
                return false;

            var top = row.TranslatePoint(new Point(), viewport).Y;
            var after = point.Y >= top + row.ActualHeight / 2;
            var sourceIndex = list.Items.IndexOf(_drag.Item);

            if (sourceIndex == targetIndex || sourceIndex == targetIndex + (after ? 1 : -1))
                return false;

            request = new ListReorderRequest(_drag.Item, list.Items[targetIndex], after);
            lineY = after ? top + row.ActualHeight : top;
            return command?.CanExecute(request) == true;
        }

        private bool UpdateDrag(Point position)
        {
            if (!TryGetViewportPoint(position, out var point))
            {
                ClearFeedback();
                return false;
            }

            _dragPosition = position;

            if (GetScrollDirection(point) != 0)
            {
                if (_scrollTimer == null)
                {
                    _scrollTimer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher)
                    {
                        Interval = TimeSpan.FromMilliseconds(150)
                    };
                    _scrollTimer.Tick += OnScrollTick;
                }

                _scrollTimer.Start();
            }
            else
            {
                _scrollTimer?.Stop();
            }

            var canDrop = TryGetDrop(point, ReorderCommand, out _, out var lineY);

            if (canDrop && CanContinueDrag())
            {
                ShowInsertLine(lineY);
                return true;
            }

            HideInsertLine();
            return false;
        }

        private int GetScrollDirection(Point point)
        {
            var viewport = _drag.Viewport;

            if (!(viewport.TemplatedParent is ScrollViewer scroll))
                return 0;

            var edge = Math.Min(ScrollEdge, viewport.ActualHeight / 2);

            if (point.Y < edge && scroll.VerticalOffset > 0)
                return -1;

            if (point.Y > viewport.ActualHeight - edge && scroll.VerticalOffset < scroll.ScrollableHeight)
            {
                var list = _drag.List;
                var lastIndex = list.Items.Count - 1;

                while (lastIndex >= 0 && !IsItemEligible(list.Items[lastIndex]))
                    lastIndex--;

                if (lastIndex >= 0)
                {
                    var row = list.ItemContainerGenerator.ContainerFromIndex(lastIndex) as ListBoxItem;

                    if (row == null || row.TranslatePoint(new Point(0, row.ActualHeight), viewport).Y > viewport.ActualHeight)
                        return 1;
                }
            }

            return 0;
        }

        private void OnScrollTick(object sender, EventArgs e)
        {
            if (TryGetViewportPoint(_dragPosition, out var point))
            {
                var direction = GetScrollDirection(point);

                if (direction != 0 && _drag.Viewport.TemplatedParent is ScrollViewer scroll)
                {
                    if (direction < 0)
                        scroll.LineUp();
                    else
                        scroll.LineDown();

                    scroll.UpdateLayout();
                }
            }

            UpdateDrag(_dragPosition);
        }

        private void ShowInsertLine(double y)
        {
            if (_insertLine == null)
            {
                _insertLayer = AdornerLayer.GetAdornerLayer(_drag.Viewport);

                if (_insertLayer == null)
                    return;

                _insertLine = new InsertAdorner(_drag.Viewport);
                _insertLayer.Add(_insertLine);
            }

            _insertLine.SetPosition(y);
        }

        private void HideInsertLine()
        {
            if (_insertLine == null)
                return;

            _insertLayer.Remove(_insertLine);
            _insertLayer = null;
            _insertLine = null;
        }

        private void ClearFeedback()
        {
            _scrollTimer?.Stop();
            HideInsertLine();
        }

        private void CancelDrag()
        {
            _dragCandidate = null;

            if (_drag != null)
                _drag.Cancelled = true;

            ClearFeedback();
            HideDragPreview();
        }

        private void EndDrag()
        {
            CancelDrag();

            if (_drag != null)
            {
                _drag.List.PreviewQueryContinueDrag -= OnQueryContinueDrag;
                _drag.List.PreviewGiveFeedback -= OnGiveFeedback;
                _drag = null;
            }
        }

        private sealed class DragToken { }

        private sealed class DragSession
        {
            public DragSession(ListBox list, ScrollContentPresenter viewport, object item)
            {
                List = list;
                Viewport = viewport;
                Item = item;
            }

            public ListBox List { get; }
            public ScrollContentPresenter Viewport { get; }
            public object Item { get; }
            public DragToken Token { get; } = new DragToken();
            public bool Cancelled { get; set; }
        }

        private sealed class InsertAdorner : Adorner
        {
            private double _y;

            public InsertAdorner(ScrollContentPresenter viewport) : base(viewport)
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
