using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace CefFlashBrowser.Utils.Behaviors
{
    public class TextBoxSelectAllOnFocus : Behavior<TextBox>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseDown += TextBoxPreviewMouseDown;
            AssociatedObject.GotFocus += TextBoxGotFocus;
            AssociatedObject.LostFocus += TextBoxLostFocus;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();
            AssociatedObject.PreviewMouseDown -= TextBoxPreviewMouseDown;
            AssociatedObject.GotFocus -= TextBoxGotFocus;
            AssociatedObject.LostFocus -= TextBoxLostFocus;
        }

        private void TextBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            AssociatedObject.Focus();
        }

        private void TextBoxGotFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            AssociatedObject.SelectAll();
            AssociatedObject.PreviewMouseDown -= TextBoxPreviewMouseDown;
        }

        private void TextBoxLostFocus(object sender, System.Windows.RoutedEventArgs e)
        {
            AssociatedObject.PreviewMouseDown += TextBoxPreviewMouseDown;
        }
    }
}
