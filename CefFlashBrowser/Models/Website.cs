using SimpleMvvm;

namespace CefFlashBrowser.Models
{
    public class Website : NotificationObject
    {
        public string Name { get; set; }
        public string Url { get; set; }

        public Website() { }

        public Website(string name, string url)
        {
            Name = name;
            Url = url;
        }

        public static bool operator ==(Website left, Website right)
        {
            return left.Name == right.Name && left.Url == right.Url;
        }

        public static bool operator !=(Website left, Website right)
        {
            return !(left == right);
        }

        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
