using CefFlashBrowser.Browser.UserScripts;
using CefFlashBrowser.Services;
using CefFlashBrowser.Services.UserScripts;
using CefFlashBrowser.Services.UserScripts.Persistence;
using SimpleMvvm.Ioc;
using System.IO;

public static class UserScriptRuntimeProbe
{
    public static UserScriptService Create(DatabaseService database, string root)
    {
        var service = new UserScriptService(new UserScriptStorage(database), Path.Combine(root, "MonkeySharp"),
            Path.Combine(root, "Caches", "UserScripts"),
            new UserScriptContentService(), new UserScriptPermissionPrompt());

        SimpleIoc.Global.Unregister<UserScriptService>(false);
        SimpleIoc.Global.Register<UserScriptService>(service);
        return service;
    }
}
