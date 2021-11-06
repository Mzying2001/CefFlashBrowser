namespace CefFlashBrowser.Models
{
    public class Website
    {
        public string Name { get; set; }
        public string Url { get; set; }

        public Website() { }

        public Website(string name, string url)
        {
            Name = name;
            Url = url;
        }
    }
}
