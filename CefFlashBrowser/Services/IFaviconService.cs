using System.ComponentModel;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    public interface IFaviconService : INotifyPropertyChanged
    {
        int Revision { get; }
        Task InitializeAsync();
        // Ignores page queries, fragments and trailing slashes. Matches pages before
        // same-origin homepages; schemeless addresses try HTTPS then HTTP at each step.
        string GetIconUrl(string pageUrl);
        Task RecordAsync(string pageUrl, string iconUrl);
        Task StopAsync();
    }
}
