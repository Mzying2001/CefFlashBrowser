using CefFlashBrowser.Models.StaticData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CefFlashBrowser.Views
{
    /// <summary>
    /// SwfPlayerWindow.xaml 的交互逻辑
    /// </summary>
    public partial class SwfPlayerWindow : Window
    {


        public string FileName
        {
            get { return (string)GetValue(FileNameProperty); }
            set { SetValue(FileNameProperty, value); }
        }

        // Using a DependencyProperty as the backing store for FileName.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty FileNameProperty =
            DependencyProperty.Register("FileName", typeof(string), typeof(SwfPlayerWindow), new PropertyMetadata(string.Empty));


        public SwfPlayerWindow()
        {
            InitializeComponent();

            var x = Settings.SwfPlayerWindowX;
            var y = Settings.SwfPlayerWindowY;
            var w = Settings.SwfPlayerWindowWidth;
            var h = Settings.SwfPlayerWindowHeight;
            if (x != default &&
                y != default &&
                w != default &&
                h != default)
            {
                Left = x;
                Top = y;
                Width = w;
                Height = h;
            }
        }

        public SwfPlayerWindow(string fileName) : this()
        {
            FileName = fileName;
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Settings.SwfPlayerWindowX = Left;
            Settings.SwfPlayerWindowY = Top;
            Settings.SwfPlayerWindowWidth = Width;
            Settings.SwfPlayerWindowHeight = Height;
        }

        public static void Show(string fileName)
        {
            new SwfPlayerWindow(fileName).Show();
        }
    }
}
