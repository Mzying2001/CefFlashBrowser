using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CefFlashBrowser.Views.CustomControls
{
    public class DropableWindow : Window
    {


        public ICommand OnDropCommand
        {
            get { return (ICommand)GetValue(OnDropCommandProperty); }
            set { SetValue(OnDropCommandProperty, value); }
        }

        // Using a DependencyProperty as the backing store for OnDropCommand.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty OnDropCommandProperty =
            DependencyProperty.Register("OnDropCommand", typeof(ICommand), typeof(DropableWindow), new PropertyMetadata(null));


        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);
            if (OnDropCommand != null)
                OnDropCommand.Execute(e);
        }
    }
}
