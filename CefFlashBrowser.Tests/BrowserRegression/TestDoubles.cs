using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Runtime.InteropServices;
using System.Threading;
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    private static extern bool IsChild(IntPtr parent, IntPtr child);

    public static bool IsForegroundWindow(IntPtr window)
    {
        return GetAncestor(GetForegroundWindow(), 3) == window;
    }

    public static bool HasFocus(IntPtr window)
    {
        var focused = GetFocus();
        return focused == window || IsChild(window, focused);
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

    public RegressionMenuModelProxy() : base(typeof(IMenuModel)) { }

    public override IMessage Invoke(IMessage message)
    {
        var call = (IMethodCallMessage)message;
        object result = true;
        switch (call.MethodName)
        {
            case "InsertItemAt":
            case "InsertCheckItemAt":
                Commands.Insert((int)call.Args[0], (CefMenuCommand)call.Args[1]);
                Labels.Insert((int)call.Args[0], (string)call.Args[2]);
                break;
            case "InsertSeparatorAt":
                Commands.Insert((int)call.Args[0], CefMenuCommand.NotFound);
                Labels.Insert((int)call.Args[0], null);
                break;
            case "Clear":
                Commands.Clear();
                Labels.Clear();
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
            case "get_XSet":
            case "get_YSet":
            case "get_WidthSet":
            case "get_HeightSet": result = 1; break;
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
