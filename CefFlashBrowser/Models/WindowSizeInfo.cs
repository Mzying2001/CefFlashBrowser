using System.Windows;

namespace CefFlashBrowser.Models
{
    public class WindowSizeInfo
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }

        public static WindowSizeInfo GetSizeInfo(Window window)
        {
            if (window.WindowState == WindowState.Normal)
            {
                return new WindowSizeInfo
                {
                    Left = window.Left,
                    Top = window.Top,
                    Width = window.Width,
                    Height = window.Height,
                    IsMaximized = false
                };
            }
            else
            {
                return new WindowSizeInfo
                {
                    Left = window.RestoreBounds.Left,
                    Top = window.RestoreBounds.Top,
                    Width = window.RestoreBounds.Width,
                    Height = window.RestoreBounds.Height,
                    IsMaximized = window.WindowState == WindowState.Maximized
                };
            }
        }

        public static void Apply(WindowSizeInfo windowSizeInfo, Window window)
        {
            if (windowSizeInfo != null)
            {
                window.Left = windowSizeInfo.Left;
                window.Top = windowSizeInfo.Top;
                window.Width = windowSizeInfo.Width;
                window.Height = windowSizeInfo.Height;

                if (windowSizeInfo.IsMaximized)
                    window.WindowState = WindowState.Maximized;
            }
        }
    }
}
