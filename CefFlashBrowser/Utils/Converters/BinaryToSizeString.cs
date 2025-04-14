using System;
using System.Globalization;

namespace CefFlashBrowser.Utils.Converters
{
    public class BinaryToSizeString : ValueConverterBase<byte[], string>
    {
        public bool UseReadableSize { get; set; }

        public override string Convert(byte[] value, object parameter, CultureInfo culture)
        {
            if (UseReadableSize)
            {
                return GetReadableSize(value);
            }
            else
            {
                return $"{value.Length} B";
            }
        }

        public override byte[] ConvertBack(string value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static string GetReadableSize(byte[] data)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };

            int order = 0;
            double len = data.Length;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }
}
