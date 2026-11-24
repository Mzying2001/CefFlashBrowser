using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace CefFlashBrowser.Views.Converters
{
    public class MonospaceFontWeightConverter : ValueConverterBase<string, FontWeight>
    {
        private readonly ConcurrentDictionary<string, FontWeight> _fontWeights =
            new ConcurrentDictionary<string, FontWeight>(StringComparer.OrdinalIgnoreCase);

        public override FontWeight Convert(string value, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value)
                ? FontWeights.Normal
                : _fontWeights.GetOrAdd(value, name => IsMonospaced(new FontFamily(name)) ? FontWeights.Bold : FontWeights.Normal);
        }

        public override string ConvertBack(FontWeight value, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private static bool IsMonospaced(FontFamily family)
        {
            var typeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            if (!typeface.TryGetGlyphTypeface(out var glyphTypeface) ||
                !glyphTypeface.CharacterToGlyphMap.TryGetValue(' ', out var spaceGlyph))
                return false;

            var width = glyphTypeface.AdvanceWidths[spaceGlyph];
            if (width <= 0)
                return false;

            for (var character = '!'; character <= '~'; character++)
            {
                if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(character, out var glyph) ||
                    glyphTypeface.AdvanceWidths[glyph] != width)
                    return false;
            }

            return true;
        }
    }
}
