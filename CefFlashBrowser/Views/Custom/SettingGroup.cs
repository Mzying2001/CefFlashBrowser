using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Views.Custom
{
    public class SettingGroup : ItemsControl
    {


        public string Header
        {
            get { return (string)GetValue(HeaderProperty); }
            set { SetValue(HeaderProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Header.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register("Header", typeof(string), typeof(SettingGroup), new PropertyMetadata(string.Empty));


        static SettingGroup()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(SettingGroup), new FrameworkPropertyMetadata(typeof(SettingGroup)));
        }
    }
}
