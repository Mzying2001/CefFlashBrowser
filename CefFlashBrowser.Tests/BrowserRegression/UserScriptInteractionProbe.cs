using CefSharp;
using Mzying2001.MonkeySharp.Core.Apis;
using Mzying2001.MonkeySharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using System.Windows.Interop;

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

public sealed class UserScriptClipboardProbe : IDisposable
{
    private sealed class ClipboardFormat
    {
        public readonly uint Id;
        public readonly TYMED Medium;
        public IntPtr Data;

        public ClipboardFormat(uint id, IntPtr source)
        {
            Id = id;
            // Display formats use the handle type of the corresponding standard format.
            var standard = id;
            switch (id)
            {
                case 0x0082: standard = 2; break;  // CF_DSPBITMAP
                case 0x0083: standard = 3; break;  // CF_DSPMETAFILEPICT
                case 0x008E: standard = 14; break; // CF_DSPENHMETAFILE
            }

            switch (standard)
            {
                case 2:                                   // CF_BITMAP
                case 9: Medium = TYMED.TYMED_GDI; break;    // CF_PALETTE
                case 3: Medium = TYMED.TYMED_MFPICT; break; // CF_METAFILEPICT
                case 14: Medium = TYMED.TYMED_ENHMF; break; // CF_ENHMETAFILE
                default: Medium = TYMED.TYMED_HGLOBAL; break;
            }

            Data = standard == 14 ? CopyEnhMetaFile(source, null) : OleDuplicateData(source, (ushort)standard, 0);
            if (Data == IntPtr.Zero)
                throw new InvalidOperationException("Could not copy clipboard format " + id + ".");
        }
    }

    private readonly List<ClipboardFormat> _formats = new List<ClipboardFormat>();

    public static UserScriptClipboardProbe Capture()
    {
        var snapshot = new UserScriptClipboardProbe();
        OpenWithRetry(IntPtr.Zero);
        try
        {
            for (uint id = EnumClipboardFormats(0); id != 0; id = EnumClipboardFormats(id))
            {
                var source = GetClipboardData(id);
                if (source == IntPtr.Zero)
                    throw new InvalidOperationException("Could not read clipboard format " + id + ".");

                snapshot._formats.Add(new ClipboardFormat(id, source));
            }

            int error = Marshal.GetLastWin32Error();
            if (error != 0)
                throw new Win32Exception(error);

            return snapshot;
        }
        catch
        {
            snapshot.Dispose();
            throw;
        }
        finally
        {
            CloseClipboard();
        }
    }

    public static void Restore(UserScriptClipboardProbe snapshot)
    {
        // Restore the original storage media. Rebuilding an IDataObject from streams
        // can make OLE try to flush embedded clipboard data with the wrong medium.
        using (snapshot)
        using (var owner = new HwndSource(new HwndSourceParameters("RegressionClipboard") { WindowStyle = 0 }))
        {
            OpenWithRetry(owner.Handle);
            try
            {
                if (!EmptyClipboard())
                    throw new Win32Exception();

                Win32Exception error = null;
                foreach (var format in snapshot._formats)
                {
                    if (SetClipboardData(format.Id, format.Data) == IntPtr.Zero)
                    {
                        error = new Win32Exception();
                        continue;
                    }

                    format.Data = IntPtr.Zero; // Windows now owns the handle.
                }

                if (error != null)
                    throw error;
            }
            finally
            {
                CloseClipboard();
            }
        }
    }

    public void Dispose()
    {
        foreach (var format in _formats)
        {
            if (format.Data == IntPtr.Zero)
                continue;

            var medium = new STGMEDIUM { tymed = format.Medium, unionmember = format.Data };
            ReleaseStgMedium(ref medium);
        }

        _formats.Clear();
    }

    private static void OpenWithRetry(IntPtr owner)
    {
        for (int attempt = 0; ; attempt++)
        {
            if (OpenClipboard(owner))
                return;

            if (attempt == 9)
                throw new Win32Exception();

            Thread.Sleep(20);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr owner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint EnumClipboardFormats(uint format);

    [DllImport("user32.dll")]
    private static extern IntPtr GetClipboardData(uint format);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint format, IntPtr data);

    [DllImport("ole32.dll")]
    private static extern IntPtr OleDuplicateData(IntPtr data, ushort format, uint flags);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CopyEnhMetaFile(IntPtr source, string fileName);
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
                    {
                        if (frame.IsValid)
                            frame.ExecuteJavaScriptAsync("document.dispatchEvent(new Event('downloadCancel'));");
                    }
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
