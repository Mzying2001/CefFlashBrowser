using System.Windows;
using System.Windows.Input;
using System.Windows.Interactivity;

namespace CefFlashBrowser.Utils.Behaviors
{
    public class DropFileBehavior : Behavior<UIElement>
    {


        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Command.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(DropFileBehavior), new PropertyMetadata(null));


        private bool _restoreAllowDrop = false;

        protected override void OnAttached()
        {
            base.OnAttached();
            _restoreAllowDrop = AssociatedObject.AllowDrop;
            AssociatedObject.AllowDrop = true;
            AssociatedObject.Drop += OnDrop;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.AllowDrop = _restoreAllowDrop;
            AssociatedObject.Drop -= OnDrop;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (Command == null || !Command.CanExecute(null))
                return;

            if (e.Data.GetData(DataFormats.FileDrop) is string[] files)
            {
                Command.Execute(files);
            }
        }
    }
}
