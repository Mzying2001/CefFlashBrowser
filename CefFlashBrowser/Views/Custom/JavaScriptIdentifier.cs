using ICSharpCode.AvalonEdit.Document;
using System.Globalization;

namespace CefFlashBrowser.Views.Custom
{
    internal static class JavaScriptIdentifier
    {
        public static int FindStart(TextDocument document, int offset)
        {
            while (ContinuesBefore(document, offset))
            {
                offset = PreviousCharacter(document, offset);
            }

            return offset;
        }

        public static int FindEnd(TextDocument document, int offset)
        {
            while (ContinuesAt(document, offset))
            {
                offset += IsSurrogatePair(document, offset) ? 2 : 1;
            }

            return offset;
        }

        public static bool CanStartAt(TextDocument document, int offset) => IsStart(GetCategory(document, offset), document.GetCharAt(offset));

        public static bool ContinuesBefore(TextDocument document, int offset) =>
            offset > 0 && ContinuesAt(document, PreviousCharacter(document, offset));

        public static bool ContinuesAt(TextDocument document, int offset) =>
            offset < document.TextLength && IsPart(GetCategory(document, offset), document.GetCharAt(offset));

        public static bool IsPart(char value) => IsPart(char.GetUnicodeCategory(value), value);

        private static bool IsPart(UnicodeCategory category, char value) =>
            IsStart(category, value) || category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark ||
            category == UnicodeCategory.DecimalDigitNumber || category == UnicodeCategory.ConnectorPunctuation ||
            value == '\u200C' || value == '\u200D' || value == '\u00B7' || value == '\u0387' ||
            (value >= '\u1369' && value <= '\u1371') || value == '\u19DA';

        private static bool IsStart(UnicodeCategory category, char value) =>
            category == UnicodeCategory.UppercaseLetter || category == UnicodeCategory.LowercaseLetter ||
            category == UnicodeCategory.TitlecaseLetter || category == UnicodeCategory.ModifierLetter ||
            category == UnicodeCategory.OtherLetter || category == UnicodeCategory.LetterNumber || value == '$' || value == '_' ||
            value == '\u1885' || value == '\u1886' || value == '\u2118' || value == '\u212E' || value == '\u309B' || value == '\u309C';

        private static UnicodeCategory GetCategory(TextDocument document, int offset) => IsSurrogatePair(document, offset)
            ? CharUnicodeInfo.GetUnicodeCategory(document.GetText(offset, 2), 0)
            : char.GetUnicodeCategory(document.GetCharAt(offset));

        private static int PreviousCharacter(TextDocument document, int offset) =>
            offset >= 2 && IsSurrogatePair(document, offset - 2) ? offset - 2 : offset - 1;

        private static bool IsSurrogatePair(TextDocument document, int offset) =>
            char.IsHighSurrogate(document.GetCharAt(offset)) && offset + 1 < document.TextLength &&
            char.IsLowSurrogate(document.GetCharAt(offset + 1));
    }
}
