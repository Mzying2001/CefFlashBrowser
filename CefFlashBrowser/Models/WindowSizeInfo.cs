using System;
using System.Windows;

namespace CefFlashBrowser.Models
{
    public class WindowSizeInfo : ICloneable
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool IsMaximized { get; set; }

        public object Clone()
        {
            return new WindowSizeInfo
            {
                Left = this.Left,
                Top = this.Top,
                Width = this.Width,
                Height = this.Height,
                IsMaximized = this.IsMaximized
            };
        }

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
            if (windowSizeInfo?.Clone() is WindowSizeInfo sizeInfo)
            {
                double desktopWidth = SystemParameters.WorkArea.Width;
                double desktopHeight = SystemParameters.WorkArea.Height;

                if (sizeInfo.Left < 0 || sizeInfo.Left + sizeInfo.Width > desktopWidth)
                {
                    sizeInfo.Left = Math.Max(0, Math.Min(sizeInfo.Left, desktopWidth - sizeInfo.Width));
                }

                if (sizeInfo.Top < 0 || sizeInfo.Top + sizeInfo.Height > desktopHeight)
                {
                    sizeInfo.Top = Math.Max(0, Math.Min(sizeInfo.Top, desktopHeight - sizeInfo.Height));
                }

                window.Left = sizeInfo.Left;
                window.Top = sizeInfo.Top;
                window.Width = sizeInfo.Width;
                window.Height = sizeInfo.Height;

                if (sizeInfo.IsMaximized)
                {
                    window.WindowState = WindowState.Maximized;
                }
            }
        }
    }
}
