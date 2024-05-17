using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Utils.Behaviors
{
    public class EscToCloseBehavior : Behavior<Window>
    {
        protected override void OnAttached()
        {
            AssociatedObject.KeyDown += KeyDownHandler;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.KeyDown -= KeyDownHandler;
        }

        private void KeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                AssociatedObject.Close();
            }
        }
    }
}
