namespace CefFlashBrowser.Models
{
    public static class MessageTokens
    {
        public const string CLOSE_WINDOW = "CLOSE_WINDOW";
        public const string EXIT_BROWSER = "EXIT_BROWSER";

        public static string CreateToken(string tokenBase, object obj)
        {
            return $"{tokenBase}-{obj.GetHashCode()}";
        }
    }
}
