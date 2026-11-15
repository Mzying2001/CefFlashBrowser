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
        if (original == null) return null;
        var copy = new DataObject();
        foreach (var format in original.GetFormats(false))
        {
            var value = original.GetData(format, false);
            var stream = value as Stream;
            if (stream != null)
            {
                var buffer = new MemoryStream();
                var position = stream.CanSeek ? stream.Position : 0;
                if (stream.CanSeek) stream.Position = 0;
                stream.CopyTo(buffer);
                if (stream.CanSeek) stream.Position = position;
                buffer.Position = 0;
                value = buffer;
            }
            else if (value is Freezable)
            {
                var freezable = ((Freezable)value).Clone();
                if (freezable.CanFreeze) freezable.Freeze();
                value = freezable;
            }
            else if (value is ICloneable)
                value = ((ICloneable)value).Clone();
            if (value != null) copy.SetData(format, value, false);
        }
        return copy;
    }

    public static void Restore(IDataObject original)
    {
        if (original == null) Clipboard.Clear();
        else Clipboard.SetDataObject(original, true);
    }
}
