using CefFlashBrowser.Utils;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace CefFlashBrowser.Views.Custom
{
    public sealed class FileIcon : Image
    {
        private static readonly ConcurrentDictionary<string, Lazy<Task<BitmapSource>>> IconCache =
            new ConcurrentDictionary<string, Lazy<Task<BitmapSource>>>(StringComparer.OrdinalIgnoreCase);

        private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

        private string _extension;

        public static readonly DependencyProperty FilePathProperty = DependencyProperty.Register(
            nameof(FilePath), typeof(string), typeof(FileIcon),
            new PropertyMetadata(null, (d, e) => ((FileIcon)d).UpdateIcon()));

        public string FilePath
        {
            get => (string)GetValue(FilePathProperty);
            set => SetValue(FilePathProperty, value);
        }

        private void UpdateIcon()
        {
            var extension = GetExtension(FilePath);

            if (StringComparer.OrdinalIgnoreCase.Equals(_extension, extension))
                return;

            _extension = extension;
            Source = null;

            if (extension != null)
            {
                _ = UpdateIconAsync(extension);
            }
        }

        private async Task UpdateIconAsync(string extension)
        {
            var icon = await GetIconAsync(extension).ConfigureAwait(false);

            _ = Dispatcher.InvokeAsync(() =>
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(_extension, extension))
                    Source = icon;
            });
        }

        private static string GetExtension(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                var fileName = Path.GetFileName(path.Trim());

                if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(InvalidFileNameChars) >= 0)
                    return null;

                return Path.GetExtension(fileName);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private static Task<BitmapSource> GetIconAsync(string extension)
        {
            return IconCache.GetOrAdd(extension, key => new Lazy<Task<BitmapSource>>(() => LoadIconAsync(key))).Value;
        }

        private static async Task<BitmapSource> LoadIconAsync(string extension)
        {
            var icon = await Task.Run(() => LoadShellIcon(extension)).ConfigureAwait(false);

            if (icon == null && extension.Length != 0)
                icon = await GetIconAsync(string.Empty).ConfigureAwait(false);

            return icon;
        }

        private static BitmapSource LoadShellIcon(string extension)
        {
            if (Win32.CoInitializeEx(IntPtr.Zero, Win32.COINIT_MULTITHREADED) < 0)
                return null;

            var fileInfo = default(Win32.SHFILEINFOW);

            try
            {
                var result = Win32.SHGetFileInfoW("file" + extension, Win32.FILE_ATTRIBUTE_NORMAL, out fileInfo,
                    (uint)Marshal.SizeOf<Win32.SHFILEINFOW>(), Win32.SHGFI_ICON | Win32.SHGFI_SMALLICON | Win32.SHGFI_USEFILEATTRIBUTES);

                if (result == UIntPtr.Zero || fileInfo.hIcon == IntPtr.Zero)
                    return null;

                var bitmap = Imaging.CreateBitmapSourceFromHIcon(fileInfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bitmap.Freeze();
                return bitmap;
            }
            catch (ExternalException)
            {
                return null;
            }
            finally
            {
                if (fileInfo.hIcon != IntPtr.Zero)
                    Win32.DestroyIcon(fileInfo.hIcon);

                Win32.CoUninitialize();
            }
        }
    }
}
