using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CefSharp;

public static class RegressionKeyboard
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)] public KeyboardInput Keyboard;
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern bool IsChild(IntPtr parent, IntPtr child);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, IntPtr processId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AttachThreadInput(uint attach, uint attachTo, bool attached);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);

    public static bool IsForegroundWindow(IntPtr window)
    {
        return GetAncestor(GetForegroundWindow(), 3) == window;
    }

    public static bool HasFocus(IntPtr window)
    {
        var focused = GetFocus();
        return focused == window || IsChild(window, focused);
    }

    public static bool TryActivateWindow(IntPtr window)
    {
        var currentThread = GetCurrentThreadId();
        var foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
        // Setup runs before Ctrl is held; attaching input queues resets key state.
        var attached = foregroundThread != 0 && foregroundThread != currentThread
            && AttachThreadInput(currentThread, foregroundThread, true);
        try
        {
            return SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
                AttachThreadInput(currentThread, foregroundThread, false);
        }
    }

    public static void SetKey(ushort virtualKey, bool released)
    {
        var input = new Input { Type = 1 };
        input.Data.Keyboard.VirtualKey = virtualKey;
        input.Data.Keyboard.Flags = released ? 2u : 0u;
        if (SendInput(1, new[] { input }, Marshal.SizeOf(typeof(Input))) != 1)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static void PressKey(ushort virtualKey)
    {
        try
        {
            SetKey(virtualKey, false);
        }
        finally
        {
            SetKey(virtualKey, true);
        }
    }

    public static void ClickMouse(int x, int y, bool right)
    {
        if (!SetCursorPos(x, y))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        var down = new Input();
        down.Data.Mouse.Flags = right ? 0x0008u : 0x0002u;
        var up = new Input();
        up.Data.Mouse.Flags = right ? 0x0010u : 0x0004u;
        if (SendInput(2, new[] { down, up }, Marshal.SizeOf(typeof(Input))) != 2)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }
}

public sealed class RegressionAddressBarRequestHandler : CefFlashBrowser.FlashBrowser.Handlers.RequestHandler
{
    private volatile string _requestedUrl;
    public string RequestedUrl { get { return _requestedUrl; } }

    public override bool OnBeforeBrowse(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
        IRequest request, bool userGesture, bool isRedirect)
    {
        if (!frame.IsMain)
            return false;

        _requestedUrl = request.Url;
        return true;
    }
}

public sealed class RegressionContextMenuParamsProxy : RealProxy
{
    public string SelectionText = "";
    public string LinkUrl = "";

    public RegressionContextMenuParamsProxy() : base(typeof(IContextMenuParams)) { }

    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result;
        switch (call.MethodName)
        {
            case "get_SelectionText": result = SelectionText; break;
            case "get_LinkUrl": result = LinkUrl; break;
            case "GetType": result = typeof(IContextMenuParams); break;
            case "ToString": result = "Regression context menu parameters"; break;
            case "GetHashCode": result = GetHashCode(); break;
            case "Equals": result = ReferenceEquals(GetTransparentProxy(), call.Args[0]); break;
            default:
                return new ReturnMessage(new InvalidOperationException(call.MethodName), call);
        }
        return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
    }
}

public sealed class RegressionMenuModelProxy : RealProxy
{
    public readonly List<CefMenuCommand> Commands = new List<CefMenuCommand> { CefMenuCommand.Copy };
    public readonly List<string> Labels = new List<string> { "Copy" };
    public readonly HashSet<CefMenuCommand> Checked = new HashSet<CefMenuCommand>();
    public readonly Dictionary<CefMenuCommand, RegressionMenuModelProxy> SubMenus = new Dictionary<CefMenuCommand, RegressionMenuModelProxy>();
    public int LimitReports;

    public RegressionMenuModelProxy() : base(typeof(IMenuModel)) { }

    public IEnumerable<CefMenuCommand> AllCommands
    {
        get
        {
            foreach (var command in Commands)
            {
                yield return command;

                RegressionMenuModelProxy submenu;
                if (SubMenus.TryGetValue(command, out submenu))
                    foreach (var child in submenu.AllCommands)
                        yield return child;
            }
        }
    }

    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result = true;
        switch (call.MethodName)
        {
            case "get_Count": result = Commands.Count; break;
            case "GetCommandIdAt": result = Commands[(int)call.Args[0]]; break;
            case "GetLabelAt": result = Labels[(int)call.Args[0]]; break;
            case "IsEnabledAt": result = true; break;
            case "IsCheckedAt": result = Checked.Contains(Commands[(int)call.Args[0]]); break;
            case "GetSubMenuAt":
                RegressionMenuModelProxy child;
                result = SubMenus.TryGetValue(Commands[(int)call.Args[0]], out child) ? child.GetTransparentProxy() : null;
                break;
            case "AddSubMenu":
                var submenu = new RegressionMenuModelProxy();
                submenu.Commands.Clear();
                submenu.Labels.Clear();
                Commands.Add((CefMenuCommand)call.Args[0]);
                Labels.Add((string)call.Args[1]);
                SubMenus.Add((CefMenuCommand)call.Args[0], submenu);
                result = submenu.GetTransparentProxy();
                break;
            case "AddItem":
                Commands.Add((CefMenuCommand)call.Args[0]);
                Labels.Add((string)call.Args[1]);
                break;
            case "AddSeparator":
                Commands.Add(CefMenuCommand.NotFound);
                Labels.Add(null);
                break;
            case "RemoveAt":
                SubMenus.Remove(Commands[(int)call.Args[0]]);
                Commands.RemoveAt((int)call.Args[0]);
                Labels.RemoveAt((int)call.Args[0]);
                break;
            case "InsertItemAt":
            case "InsertCheckItemAt":
                Commands.Insert((int)call.Args[0], (CefMenuCommand)call.Args[1]);
                Labels.Insert((int)call.Args[0], (string)call.Args[2]);
                break;
            case "InsertSeparatorAt":
                Commands.Insert((int)call.Args[0], CefMenuCommand.NotFound);
                Labels.Insert((int)call.Args[0], null);
                break;
            case "SetChecked":
                if ((bool)call.Args[1]) {
                    Checked.Add((CefMenuCommand)call.Args[0]);
                } else {
                    Checked.Remove((CefMenuCommand)call.Args[0]);
                }
                break;
            case "IsChecked":
                result = Checked.Contains((CefMenuCommand)call.Args[0]);
                break;
            case "Clear":
                Commands.Clear();
                Labels.Clear();
                SubMenus.Clear();
                Checked.Clear();
                break;
            case "GetType": result = typeof(IMenuModel); break;
            case "ToString": result = "Regression menu model"; break;
            case "GetHashCode": result = GetHashCode(); break;
            case "Equals": result = ReferenceEquals(GetTransparentProxy(), call.Args[0]); break;
            default:
                return new ReturnMessage(new InvalidOperationException(call.MethodName), call);
        }
        return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
    }
}

public sealed class DisposedBrowserProxy : RealProxy
{
    public DisposedBrowserProxy(Type type)
        : base(type)
    {
    }

    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result;

        switch (call.MethodName)
        {
            case "get_IsDisposed":
                result = true;
                break;
            case "GetType":
                result = GetProxiedType();
                break;
            case "ToString":
                result = "Disposed browser proxy";
                break;
            case "GetHashCode":
                result = GetHashCode();
                break;
            case "Equals":
                result = ReferenceEquals(GetTransparentProxy(), call.Args[0]);
                break;
            default:
                return new ReturnMessage(new ObjectDisposedException("test browser"), call);
        }
        return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
    }
}

public sealed class DisposedDuringCallbackBrowserProxy : RealProxy
{
    public DisposedDuringCallbackBrowserProxy() : base(typeof(IBrowser))
    {
    }

    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result;

        switch (call.MethodName)
        {
            case "get_IsDisposed": result = false; break;
            case "get_IsPopup": result = false; break;
            case "GetType": result = typeof(IBrowser); break;
            case "ToString": result = "Browser disposed during callback"; break;
            case "GetHashCode": result = GetHashCode(); break;
            case "Equals": result = ReferenceEquals(GetTransparentProxy(), call.Args[0]); break;
            default:
                return new ReturnMessage(new ObjectDisposedException("test browser"), call);
        }

        return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
    }
}

public sealed class RegressionMainFrameProxy : RealProxy
{
    public RegressionMainFrameProxy() : base(typeof(IFrame))
    {
    }

    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result;

        switch (call.MethodName)
        {
            case "get_IsMain": result = true; break;
            case "GetType": result = typeof(IFrame); break;
            case "ToString": result = "Regression main frame"; break;
            case "GetHashCode": result = GetHashCode(); break;
            case "Equals": result = ReferenceEquals(GetTransparentProxy(), call.Args[0]); break;
            default:
                var returnType = ((System.Reflection.MethodInfo)call.MethodBase).ReturnType;
                result = returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
                break;
        }

        return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
    }
}

public sealed class RegressionPopupFeaturesProxy : RealProxy
{
    public RegressionPopupFeaturesProxy() : base(typeof(IPopupFeatures)) { }

    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result;
        switch (call.MethodName)
        {
            case "GetType": result = typeof(IPopupFeatures); break;
            case "ToString": result = "Regression popup features"; break;
            case "GetHashCode": result = GetHashCode(); break;
            case "Equals": result = ReferenceEquals(GetTransparentProxy(), call.Args[0]); break;
            case "get_X": result = 20; break;
            case "get_Y": result = 30; break;
            case "get_Width": result = 500; break;
            case "get_Height": result = 360; break;
            default:
                return new ReturnMessage(new InvalidOperationException(call.MethodName), call);
        }
        return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
    }
}

public sealed class RegressionJsDialogHandler : CefFlashBrowser.Browser.Handlers.JsDialogHandler
{
    public volatile bool Accept;
    public volatile bool Hold;
    public int Count;
    private IJsDialogCallback pendingCallback;
    private IWebBrowser pendingBrowser;
    private bool pendingIsReload;

    public override bool OnBeforeUnloadDialog(IWebBrowser control, IBrowser browser,
        string message, bool isReload, IJsDialogCallback callback)
    {
        RegisterBeforeUnloadDialog(control, callback, isReload);

        if (Hold)
        {
            pendingBrowser = control;
            pendingIsReload = isReload;
            pendingCallback = callback;
            Interlocked.Increment(ref Count);
            return true;
        }

        Interlocked.Increment(ref Count);
        CompleteBeforeUnloadDialog(control, callback, isReload, Accept);

        return true;
    }

    public void CompletePending(bool accepted)
    {
        var callback = Interlocked.Exchange(ref pendingCallback, null);
        if (callback != null)
            CompleteBeforeUnloadDialog(pendingBrowser, callback, pendingIsReload, accepted);
    }
}

public static class RegressionAudio
{
    public static Task<bool> IsMutedAsync(IBrowser browser, TaskFactory cefTaskFactory)
    {
        return cefTaskFactory.StartNew(() => browser.GetHost().IsAudioMuted);
    }
}

public sealed class RegressionAudioHostProxy : RealProxy
{
    private readonly bool _muted;

    public RegressionAudioHostProxy(bool muted) : base(typeof(IBrowserHost))
    {
        _muted = muted;
    }

    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result = true;
        switch (call.MethodName)
        {
            case "get_IsAudioMuted":
                result = _muted;
                break;
            case "get_IsDisposed":
                result = false;
                break;
            case "GetType":
                result = typeof(IBrowserHost);
                break;
            case "ToString":
                result = "Regression audio host proxy";
                break;
            case "GetHashCode":
                result = GetHashCode();
                break;
            case "Equals":
                result = ReferenceEquals(GetTransparentProxy(), call.Args[0]);
                break;
            default:
                return new ReturnMessage(new InvalidOperationException(call.MethodName), call);
        }
        return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
    }
}

public sealed class RegressionAudioBrowserProxy : RealProxy
{
    private readonly object _host;

    public RegressionAudioBrowserProxy(object host) : base(typeof(IBrowser))
    {
        _host = host;
    }

    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result = true;
        switch (call.MethodName)
        {
            case "GetHost":
                result = _host;
                break;
            case "GetType":
                result = typeof(IBrowser);
                break;
            case "ToString":
                result = "Regression audio browser proxy";
                break;
            case "GetHashCode":
                result = GetHashCode();
                break;
            case "Equals":
                result = ReferenceEquals(GetTransparentProxy(), call.Args[0]);
                break;
            default:
                return new ReturnMessage(new InvalidOperationException(call.MethodName), call);
        }
        return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
    }
}
