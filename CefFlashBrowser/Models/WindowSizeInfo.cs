using System.Windows;

namespace CefFlashBrowser.Models
{
    public class WindowSizeInfo
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public static WindowSizeInfo GetSizeInfo(Window window)
        {
            return new WindowSizeInfo
            {
                Left = window.Left,
                Top = window.Top,
                Width = window.Width,
                Height = window.Height
            };
        }

        public void Apply(Window window)
        {
            window.Left = Left;
            window.Top = Top;
            window.Width = Width;
            window.Height = Height;
        }
    }
}
