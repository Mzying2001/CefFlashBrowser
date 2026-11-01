using CefFlashBrowser.Utils;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CefFlashBrowser.Views.Custom
{
    [TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
    [TemplatePart(Name = "PART_DragPreview", Type = typeof(Popup))]
    [TemplatePart(Name = "PART_DragPreviewImage", Type = typeof(Image))]
    public class BrowserTab : ListBox
    {
        private const double DefaultItemWidth = 250;
        private const double MinimumItemWidth = 80;

        public static readonly ComponentResourceKey ItemContainerStyleKey =
            new ComponentResourceKey(typeof(BrowserTab), "ItemContainerStyle");

        public static readonly DependencyProperty IsDragEnabledProperty =
            DependencyProperty.Register(nameof(IsDragEnabled), typeof(bool), typeof(BrowserTab), new PropertyMetadata(false, OnIsDragEnabledChanged));

        private static readonly DependencyPropertyKey ActualItemWidthPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(ActualItemWidth), typeof(double), typeof(BrowserTab), new FrameworkPropertyMetadata(DefaultItemWidth));

        public static readonly DependencyProperty ActualItemWidthProperty =
            ActualItemWidthPropertyKey.DependencyProperty;

        private static readonly DependencyPropertyKey DraggedItemPropertyKey =
            DependencyProperty.RegisterReadOnly(nameof(DraggedItem), typeof(object), typeof(BrowserTab), new PropertyMetadata(null));

        public static readonly DependencyProperty DraggedItemProperty =
            DraggedItemPropertyKey.DependencyProperty;

        /// <summary>
        /// Requests a synchronous collection move. The control never modifies its items.
        /// </summary>
        public static readonly RoutedEvent MoveItemEvent = EventManager.RegisterRoutedEvent(
            nameof(MoveItem), RoutingStrategy.Bubble, typeof(MoveItemEventHandler), typeof(BrowserTab));

        /// <summary>Enables reordering gestures. Disabled by default.</summary>
        public bool IsDragEnabled
        {
            get => (bool)GetValue(IsDragEnabledProperty);
            set => SetValue(IsDragEnabledProperty, value);
        }

        /// <summary>The equal item width calculated from the available layout space.</summary>
        public double ActualItemWidth => (double)GetValue(ActualItemWidthProperty);

        /// <summary>The item shown in the drag preview, or null when no preview is active.</summary>
        public object DraggedItem => GetValue(DraggedItemProperty);

        public event MoveItemEventHandler MoveItem
        {
            add => AddHandler(MoveItemEvent, value);
            remove => RemoveHandler(MoveItemEvent, value);
        }

        private ScrollViewer _scrollViewer;
        private Popup _dragPreview;
        private Image _dragPreviewImage;
        private object _draggedItem;
        private Point _dragStart;
        private Point _dragOffset;
        private Point _dragPosition;
        private bool _isDragging;
        private DispatcherTimer _dragScrollTimer;

        static BrowserTab()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BrowserTab), new FrameworkPropertyMetadata(typeof(BrowserTab)));
        }

        public BrowserTab()
        {
            Unloaded += (s, e) => CancelDrag();
            IsEnabledChanged += (s, e) => { if (!IsEnabled) CancelDrag(); };
            IsVisibleChanged += (s, e) => { if (!IsVisible) CancelDrag(); };
        }

        private static void OnIsDragEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
                ((BrowserTab)d).CancelDrag();
        }

        public override void OnApplyTemplate()
        {
            CancelDrag();

            if (_dragPreview != null)
                _dragPreview.Opened -= DragPreviewOpened;

            base.OnApplyTemplate();

            _scrollViewer = GetTemplateChild("PART_ScrollViewer") as ScrollViewer;
            _dragPreview = GetTemplateChild("PART_DragPreview") as Popup;
            _dragPreviewImage = GetTemplateChild("PART_DragPreviewImage") as Image;

            if (_dragPreview != null)
                _dragPreview.Opened += DragPreviewOpened;
        }

        protected override Size MeasureOverride(Size constraint)
        {
            // Use the measure constraint, not ActualWidth, so a short strip can grow again.
            var availableWidth = Math.Max(0, constraint.Width - Padding.Left - Padding.Right
                - BorderThickness.Left - BorderThickness.Right);

            var width = Items.Count == 0 ? DefaultItemWidth
                : Math.Max(MinimumItemWidth, Math.Min(DefaultItemWidth, availableWidth / Items.Count));

            SetValue(ActualItemWidthPropertyKey, width);
            return base.MeasureOverride(constraint);
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            InvalidateMeasure();

            if (_draggedItem != null && !Items.Contains(_draggedItem))
                CancelDrag();
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            if (SelectedItem != null)
                ScrollIntoView(SelectedItem);
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseDown(e);

            if (e.ChangedButton != MouseButton.Left || _isDragging)
                return;

            _draggedItem = null;

            if (!IsDragEnabled || !IsEnabled
                || !(ContainerFromElement(this, e.OriginalSource as DependencyObject) is ListBoxItem tab)
                || ItemsControlFromItemContainer(tab) != this)
                return;

            // Buttons retain their own gesture and mouse capture.
            for (var source = e.OriginalSource as DependencyObject; source != null && source != tab;)
            {
                if (source is ButtonBase)
                    return;

                source = source is Visual ? VisualTreeHelper.GetParent(source)
                    : source is FrameworkContentElement content ? content.Parent : LogicalTreeHelper.GetParent(source);
            }

            _draggedItem = ItemContainerGenerator.ItemFromContainer(tab);
            _dragStart = e.GetPosition(this);
            _dragOffset = e.GetPosition(tab);
        }

        private bool CanDrag => IsDragEnabled && IsEnabled && IsVisible && IsLoaded
            && _scrollViewer != null && _draggedItem != null && Items.Contains(_draggedItem);

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (_isDragging || _draggedItem == null)
                return;

            if (e.LeftButton != MouseButtonState.Pressed || !CanDrag)
            {
                CancelDrag();
                return;
            }

            var position = e.GetPosition(this);

            if (Math.Abs(position.X - _dragStart.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(position.Y - _dragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            e.Handled = true;
            SetCurrentValue(SelectedItemProperty, _draggedItem);

            if (!CanDrag)
                return;

            _isDragging = true;
            _dragPosition = position;
            _dragScrollTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Input,
                (s, args) => UpdateTabDrag(_dragPosition, true), Dispatcher);

            try
            {
                ShowTabDragPreview();
                // A moved item may lose its container; use the stable list as the source.
                DragDrop.DoDragDrop(this, new DataObject(typeof(TabDragData), new TabDragData(this, _draggedItem)), DragDropEffects.Move);
            }
            finally
            {
                CancelDrag();
                _isDragging = false;
            }
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            if (!_isDragging)
                _draggedItem = null;
        }

        private bool IsTabDrag(DragEventArgs e)
        {
            return _isDragging && CanDrag && e.Data.GetData(typeof(TabDragData)) is TabDragData data
                && ReferenceEquals(data.Owner, this) && ReferenceEquals(data.Item, _draggedItem);
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);

            if (!IsDragEnabled)
                return;

            e.Effects = IsTabDrag(e) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;

            if (e.Effects == DragDropEffects.Move)
            {
                _dragPosition = e.GetPosition(this);
                UpdateTabDrag(_dragPosition, false);
            }
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            OnDragOver(e);
        }

        protected override void OnDragLeave(DragEventArgs e)
        {
            base.OnDragLeave(e);
            _dragPosition = new Point(-1, -1);
        }

        private void ShowTabDragPreview()
        {
            UpdateLayout();

            if (_dragPreview == null || _dragPreviewImage == null
                || !(ItemContainerGenerator.ContainerFromItem(_draggedItem) is ListBoxItem tab)
                || VisualTreeHelper.GetChildrenCount(tab) == 0 || tab.ActualWidth <= 0 || tab.ActualHeight <= 0)
                return;

            // Capture the template root before dimming, without the container's layout offset.
            var dpi = VisualTreeHelper.GetDpi(tab);
            var snapshot = new RenderTargetBitmap(
                Math.Max(1, (int)Math.Ceiling(tab.ActualWidth * dpi.DpiScaleX)),
                Math.Max(1, (int)Math.Ceiling(tab.ActualHeight * dpi.DpiScaleY)),
                dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
            snapshot.Render((Visual)VisualTreeHelper.GetChild(tab, 0));
            snapshot.Freeze();

            _dragPreviewImage.Source = snapshot;
            _dragPreviewImage.Width = tab.ActualWidth;
            _dragPreviewImage.Height = tab.ActualHeight;
            _dragPreview.HorizontalOffset = _dragPosition.X - _dragOffset.X - 8;
            _dragPreview.VerticalOffset = _dragPosition.Y - _dragOffset.Y - 8;
            _dragPreview.IsOpen = true;
            SetValue(DraggedItemPropertyKey, _draggedItem);
        }

        private void HideTabDragPreview()
        {
            if (_dragPreview != null)
                _dragPreview.IsOpen = false;

            if (_dragPreviewImage != null)
                _dragPreviewImage.Source = null;

            SetValue(DraggedItemPropertyKey, null);
        }

        private void CancelDrag()
        {
            _dragScrollTimer?.Stop();
            _dragScrollTimer = null;

            HideTabDragPreview();

            _draggedItem = null;

            if (_isDragging)
                Mouse.SetCursor(null);
        }

        private void DragPreviewOpened(object sender, EventArgs e)
        {
            if (PresentationSource.FromVisual(_dragPreview.Child) is HwndSource source)
            {
                // Let OLE drag hit testing reach the strip beneath the native popup.
                HwndHelper.SetWindowExStyle(source.Handle, HwndHelper.GetWindowExStyle(source.Handle)
                    | Win32.WS_EX_TRANSPARENT | Win32.WS_EX_NOACTIVATE);

                UpdateTabDragPreview();
            }
        }

        private void UpdateTabDragPreview()
        {
            if (_dragPreview != null && _dragPreview.IsOpen
                && PresentationSource.FromVisual(_dragPreview.Child) is HwndSource source
                && Win32.GetCursorPos(out var cursor))
            {
                var offset = source.CompositionTarget.TransformToDevice.Transform(
                    new Vector(_dragOffset.X + 8, _dragOffset.Y + 8));

                Win32.SetWindowPos(source.Handle, IntPtr.Zero,
                    cursor.x - (int)offset.X, cursor.y - (int)offset.Y, 0, 0,
                    Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
            }
        }

        protected override void OnGiveFeedback(GiveFeedbackEventArgs e)
        {
            base.OnGiveFeedback(e);

            if (!_isDragging)
                return;

            UpdateTabDragPreview();

            if (e.Effects == DragDropEffects.Move)
            {
                e.UseDefaultCursors = false;
                Mouse.SetCursor(Cursors.Arrow);
                e.Handled = true;
            }
        }

        protected override void OnQueryContinueDrag(QueryContinueDragEventArgs e)
        {
            base.OnQueryContinueDrag(e);

            if (_isDragging && !CanDrag)
            {
                e.Action = DragAction.Cancel;
                e.Handled = true;
            }
        }

        private void UpdateTabDrag(Point position, bool scroll)
        {
            if (!CanDrag)
                return;

            var viewportPosition = TranslatePoint(position, _scrollViewer);

            if (viewportPosition.Y < 0 || viewportPosition.Y > _scrollViewer.ActualHeight
                || viewportPosition.X < 0 || viewportPosition.X > _scrollViewer.ActualWidth)
                return;

            if (scroll)
            {
                const double edgeWidth = 24;

                if (viewportPosition.X < edgeWidth)
                    _scrollViewer.LineLeft();

                else if (viewportPosition.X > _scrollViewer.ActualWidth - edgeWidth)
                    _scrollViewer.LineRight();

                UpdateLayout();

                if (!CanDrag)
                    return;
            }

            var oldIndex = Items.IndexOf(_draggedItem);
            var newIndex = oldIndex;

            for (int i = 0; i < Items.Count; i++)
            {
                if (i == oldIndex || !(ItemContainerGenerator.ContainerFromIndex(i) is ListBoxItem tab))
                    continue;

                var center = tab.TranslatePoint(new Point(tab.ActualWidth / 2, 0), this).X;

                if (i < oldIndex && position.X < center)
                {
                    newIndex = i;
                    break;
                }

                if (i > oldIndex && position.X > center)
                    newIndex = i;
            }

            if (oldIndex != newIndex)
                RaiseEvent(new MoveItemEventArgs(MoveItemEvent, this, _draggedItem, oldIndex, newIndex));
        }

        protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);

            if (_scrollViewer == null)
                return;

            if (e.Delta > 0)
                _scrollViewer.LineLeft();

            else if (e.Delta < 0)
                _scrollViewer.LineRight();

            e.Handled = true;
        }

        private sealed class TabDragData
        {
            public BrowserTab Owner { get; }
            public object Item { get; }

            public TabDragData(BrowserTab owner, object item)
            {
                Owner = owner;
                Item = item;
            }
        }
    }

    public class MoveItemEventArgs : RoutedEventArgs
    {
        public object Item { get; }
        public int OldIndex { get; }

        /// <summary>The item's final index, using ObservableCollection.Move semantics.</summary>
        public int NewIndex { get; }

        public MoveItemEventArgs(RoutedEvent routedEvent, object source, object item, int oldIndex, int newIndex)
            : base(routedEvent, source)
        {
            Item = item;
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }
    }

    public delegate void MoveItemEventHandler(object sender, MoveItemEventArgs e);
}
