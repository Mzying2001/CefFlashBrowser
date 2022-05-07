using CefSharp;
using System.Windows.Input;
using System.Windows.Threading;

namespace CefFlashBrowser.WinformCefSharp4WPF
{
    public interface IWpfWebBrowser : IWebBrowser
    {
        ICommand BackCommand { get; }

        ICommand ForwardCommand { get; }

        ICommand ReloadCommand { get; }

        ICommand PrintCommand { get; }

        ICommand ZoomInCommand { get; }

        ICommand ZoomOutCommand { get; }

        ICommand ZoomResetCommand { get; }

        ICommand ViewSourceCommand { get; }

        ICommand StopCommand { get; }

        ICommand CutCommand { get; }

        ICommand CopyCommand { get; }

        ICommand PasteCommand { get; }

        ICommand SelectAllCommand { get; }

        ICommand UndoCommand { get; }

        ICommand RedoCommand { get; }

        Dispatcher Dispatcher { get; }

        double ZoomLevel { get; set; }

        double ZoomLevelIncrement { get; set; }

        string Title { get; }
    }
}