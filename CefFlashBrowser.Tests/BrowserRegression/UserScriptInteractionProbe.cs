using CefSharp;
using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Domain;
using System;
using System.IO;
using System.Windows;

public sealed class UserScriptNotificationProbe
{
    public readonly INotificationHandle Handle;
    public int Clicks, Closes;

    public UserScriptNotificationProbe(INotificationService service, ScriptKey key)
    {
        Handle = service.Create(new UserScriptNotificationRequest(key, "Unstarted notification probe", "Lifecycle fixture", null, false, true, null), System.Threading.CancellationToken.None);
        Handle.Clicked += (sender, e) => Clicks++;
        Handle.Closed += (sender, e) => Closes++;
    }
}

public static class UserScriptClipboardProbe
{
    public static IDataObject Capture()
    {
        var original = Clipboard.GetDataObject();

        if (original == null)
            return null;

        var copy = new DataObject();

        foreach (var format in original.GetFormats(false))
        {
            var value = original.GetData(format, false);
            var stream = value as Stream;

            if (stream != null)
            {
                var buffer = new MemoryStream();
                var position = stream.CanSeek ? stream.Position : 0;

                if (stream.CanSeek)
                    stream.Position = 0;

                stream.CopyTo(buffer);

                if (stream.CanSeek)
                    stream.Position = position;

                buffer.Position = 0;
                value = buffer;
            }
            else if (value is Freezable)
            {
                var freezable = ((Freezable)value).Clone();

                if (freezable.CanFreeze)
                    freezable.Freeze();

                value = freezable;
            }
            else if (value is ICloneable)
                value = ((ICloneable)value).Clone();

            if (value != null)
                copy.SetData(format, value, false);
        }

        return copy;
    }

    public static void Restore(IDataObject original)
    {
        if (original == null)
            Clipboard.Clear();
        else
            Clipboard.SetDataObject(original, true);
    }
}

public sealed class UserScriptDownloadProbe
{
    public readonly IDownloadOperation Operation;
    public int Completed, Aborted, TimedOut, Failed;

    public UserScriptDownloadProbe(IDownloadService service, ScriptKey key, string url)
    {
        Operation = service.CreateAsync(new DownloadRequest(key, new Uri(url), "contract.bin", false), System.Threading.CancellationToken.None).GetAwaiter().GetResult();
        Operation.Completed += (sender, e) => System.Threading.Interlocked.Increment(ref Completed);
        Operation.Aborted += (sender, e) => System.Threading.Interlocked.Increment(ref Aborted);
        Operation.TimedOut += (sender, e) => System.Threading.Interlocked.Increment(ref TimedOut);
        Operation.Failed += (sender, e) => System.Threading.Interlocked.Increment(ref Failed);
    }
}

public sealed class UserScriptSaveDialogProbe : IDisposable
{
    private readonly IWebBrowser browser;
    private readonly string title;
    private readonly System.Threading.Timer timer;
    private int seen;

    public bool Seen
    {
        get
        {
            return System.Threading.Volatile.Read(ref seen) != 0;
        }
    }

    public UserScriptSaveDialogProbe(IWebBrowser browser, string title)
    {
        this.browser = browser;
        this.title = title;
        timer = new System.Threading.Timer(Check, null, 0, 20);
    }

    private void Check(object state)
    {
        if (Seen)
            return;

        EnumWindows((window, parameter) =>
        {
            uint process;

            GetWindowThreadProcessId(window, out process);

            if (process != (uint)System.Diagnostics.Process.GetCurrentProcess().Id)
                return true;

            var text = new System.Text.StringBuilder(256);
            var kind = new System.Text.StringBuilder(64);

            GetWindowText(window, text, text.Capacity);
            GetClassName(window, kind, kind.Capacity);

            if (kind.ToString() != "#32770" || text.ToString() != title)
                return true;

            if (System.Threading.Interlocked.Exchange(ref seen, 1) == 0)
                Cef.UIThreadTaskFactory.StartNew(() =>
                {
                    if (browser.IsDisposed)
                        return;

                    using (var frame = browser.GetBrowser().MainFrame)
                        if (frame.IsValid)
                            frame.ExecuteJavaScriptAsync("document.dispatchEvent(new Event('downloadCancel'));");
                });

            return false;
        }, IntPtr.Zero);
    }

    public void Dispose()
    {
        timer.Dispose();
    }

    private delegate bool Callback(IntPtr window, IntPtr parameter);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool EnumWindows(Callback callback, IntPtr parameter);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr window, System.Text.StringBuilder text, int count);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window, System.Text.StringBuilder text, int count);
}
