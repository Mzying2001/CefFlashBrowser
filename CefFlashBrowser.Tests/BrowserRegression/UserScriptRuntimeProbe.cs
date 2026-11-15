using CefFlashBrowser.Browser.UserScripts;
using CefFlashBrowser.Database;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Services.UserScripts.Persistence;
using SimpleMvvm.Ioc;
using System.IO;

public static class UserScriptRuntimeProbe
{
    public static UserScriptService Create(IDatabase database, DatabaseService schema, string root)
    {
        var service = new UserScriptService(new UserScriptStorage(database, schema), Path.Combine(root, "MonkeySharp"),
            new UserScriptContentService(), new UserScriptPermissionPrompt());
        SimpleIoc.Global.Unregister<UserScriptService>(false);
        SimpleIoc.Global.Register<UserScriptService>(service);
        return service;
    }
}
