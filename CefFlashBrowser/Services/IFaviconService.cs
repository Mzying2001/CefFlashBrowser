using System.ComponentModel;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    public interface IFaviconService : INotifyPropertyChanged
    {
        int Revision { get; }
        Task InitializeAsync();
        // Ignores page queries, fragments and trailing slashes. Only schemeless
        // addresses can fall back to the site's homepage or latest updated icon.
        string GetIconUrl(string pageUrl);
        Task RecordAsync(string pageUrl, string iconUrl);
        Task StopAsync();
    }
}
