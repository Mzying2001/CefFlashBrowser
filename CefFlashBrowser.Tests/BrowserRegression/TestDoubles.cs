using System;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using System.Threading;
using CefSharp;

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
