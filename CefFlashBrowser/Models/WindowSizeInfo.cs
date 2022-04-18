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

        public static void Apply(WindowSizeInfo windowSizeInfo, Window window)
        {
            if (windowSizeInfo != null)
            {
                window.Left = windowSizeInfo.Left;
                window.Top = windowSizeInfo.Top;
                window.Width = windowSizeInfo.Width;
                window.Height = windowSizeInfo.Height;
            }
        }
    }
}
