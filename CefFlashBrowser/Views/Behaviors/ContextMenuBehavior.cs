using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CefFlashBrowser.Views.Behaviors
{
    public class ContextMenuBehavior : Behavior<ButtonBase>
    {
        private bool _isAttached;
        private bool _attachPending;
        private int _attachGeneration;

        public static readonly DependencyProperty MenuProperty =
            DependencyProperty.Register(
                nameof(Menu),
                typeof(ContextMenu),
                typeof(ContextMenuBehavior),
                new PropertyMetadata(null, OnMenuChanged));

        public static readonly DependencyProperty HorizontalAlignmentProperty =
            DependencyProperty.Register(
                nameof(HorizontalAlignment),
                typeof(HorizontalAlignment),
                typeof(ContextMenuBehavior),
                new PropertyMetadata(HorizontalAlignment.Right));

        public static readonly DependencyProperty VerticalAlignmentProperty =
            DependencyProperty.Register(
                nameof(VerticalAlignment),
                typeof(VerticalAlignment),
                typeof(ContextMenuBehavior),
                new PropertyMetadata(VerticalAlignment.Bottom));

        public static readonly DependencyProperty HorizontalOffsetProperty =
            DependencyProperty.Register(
                nameof(HorizontalOffset),
                typeof(double),
                typeof(ContextMenuBehavior),
                new PropertyMetadata(0d));

        public static readonly DependencyProperty VerticalOffsetProperty =
            DependencyProperty.Register(
                nameof(VerticalOffset),
                typeof(double),
                typeof(ContextMenuBehavior),
                new PropertyMetadata(0d));

        public ContextMenu Menu
        {
            get => (ContextMenu)GetValue(MenuProperty);
            set => SetValue(MenuProperty, value);
        }

        public HorizontalAlignment HorizontalAlignment
        {
            get => (HorizontalAlignment)GetValue(HorizontalAlignmentProperty);
            set => SetValue(HorizontalAlignmentProperty, value);
        }

        public VerticalAlignment VerticalAlignment
        {
            get => (VerticalAlignment)GetValue(VerticalAlignmentProperty);
            set => SetValue(VerticalAlignmentProperty, value);
        }

        public double HorizontalOffset
        {
            get => (double)GetValue(HorizontalOffsetProperty);
            set => SetValue(HorizontalOffsetProperty, value);
        }

        public double VerticalOffset
        {
            get => (double)GetValue(VerticalOffsetProperty);
            set => SetValue(VerticalOffsetProperty, value);
        }

        protected override void OnAttached()
        {
            base.OnAttached();

            var attachGeneration = ++_attachGeneration;
            _attachPending = true;
            var associatedObject = AssociatedObject;

            // Resolve DynamicResource menus after the XAML namescope has been built.
            associatedObject.Dispatcher.InvokeAsync(() =>
            {
                if (!_attachPending || attachGeneration != _attachGeneration || !ReferenceEquals(AssociatedObject, associatedObject))
                    return;

                _attachPending = false;
                _isAttached = true;
                associatedObject.Click += OnClick;
                AttachMenu(Menu);
            });
        }

        protected override void OnDetaching()
        {
            _attachPending = false;
            _attachGeneration++;

            if (_isAttached)
            {
                AssociatedObject.Click -= OnClick;
                DetachMenu(Menu);
            }

            if (Menu?.PlacementTarget == AssociatedObject)
            {
                Menu.IsOpen = false;
                Menu.PlacementTarget = null;
            }

            _isAttached = false;
            base.OnDetaching();
        }

        private static void OnMenuChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
        {
            var behavior = (ContextMenuBehavior)dependencyObject;

            if (!behavior._isAttached)
                return;

            var oldMenu = e.OldValue as ContextMenu;
            if (oldMenu?.PlacementTarget == behavior.AssociatedObject)
            {
                oldMenu.IsOpen = false;
                oldMenu.PlacementTarget = null;
            }

            behavior.DetachMenu(oldMenu);
            behavior.AttachMenu(e.NewValue as ContextMenu);
        }

        private void AttachMenu(ContextMenu menu)
        {
            if (menu != null)
                menu.Opened += OnMenuOpened;
        }

        private void DetachMenu(ContextMenu menu)
        {
            if (menu != null)
                menu.Opened -= OnMenuOpened;
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            if (Menu == null)
                return;

            Menu.PlacementTarget = AssociatedObject;
            Menu.Placement = PlacementMode.Relative;
            UpdatePlacementRectangle(Menu);
            Menu.IsOpen = true;
        }

        private void OnMenuOpened(object sender, RoutedEventArgs e)
        {
            var menu = (ContextMenu)sender;

            AssociatedObject.Dispatcher.InvokeAsync(() =>
            {
                if (menu.IsOpen && menu.PlacementTarget == AssociatedObject)
                    UpdatePlacementRectangle(menu);
            });
        }

        private void UpdatePlacementRectangle(ContextMenu menu)
        {
            var targetSize = AssociatedObject.RenderSize;
            var menuSize = menu.RenderSize;

            var x = GetHorizontalPosition(targetSize.Width, menuSize.Width);
            var y = GetVerticalPosition(targetSize.Height, menuSize.Height);

            menu.PlacementRectangle = new Rect(x + HorizontalOffset, y + VerticalOffset, 0, 0);
        }

        private double GetHorizontalPosition(double targetWidth, double menuWidth)
        {
            switch (HorizontalAlignment)
            {
                case HorizontalAlignment.Center:
                    return (targetWidth - menuWidth) / 2;

                case HorizontalAlignment.Right:
                    return targetWidth - menuWidth;

                default:
                    return 0;
            }
        }

        private double GetVerticalPosition(double targetHeight, double menuHeight)
        {
            switch (VerticalAlignment)
            {
                case VerticalAlignment.Top:
                    return -menuHeight;

                case VerticalAlignment.Center:
                    return (targetHeight - menuHeight) / 2;

                case VerticalAlignment.Bottom:
                    return targetHeight;
                default:
                    return targetHeight;
            }
        }
    }
}
