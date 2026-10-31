using System.ComponentModel;
using System.Threading.Tasks;

namespace CefFlashBrowser.Services
{
    public interface IFaviconService : INotifyPropertyChanged
    {
        int Revision { get; }
        Task InitializeAsync();
        string GetIconUrl(string pageUrl);
        Task RecordAsync(string pageUrl, string iconUrl);
        Task StopAsync();
    }
}
