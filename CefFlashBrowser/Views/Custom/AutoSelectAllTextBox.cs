using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CefFlashBrowser.Views.Custom
{
    public class AutoSelectAllTextBox : TextBox
    {
        public override void BeginInit()
        {
            base.BeginInit();
            PreviewMouseDown += AutoSelectAllTextBox_PreviewMouseDown;
        }

        private void AutoSelectAllTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            Focus();
            e.Handled = true;
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            SelectAll();
            PreviewMouseDown -= AutoSelectAllTextBox_PreviewMouseDown;
        }

        protected override void OnLostFocus(RoutedEventArgs e)
        {
            base.OnLostFocus(e);
            PreviewMouseDown += AutoSelectAllTextBox_PreviewMouseDown;
        }
    }
}
