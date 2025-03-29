using CefFlashBrowser.Sol;
using CefFlashBrowser.Utils;
using System.Collections.Generic;

namespace CefFlashBrowser.Models
{
    public class SolObject
    {
        public string ClassName { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public SolObject()
        {
            ClassName = string.Empty;
            Properties = new Dictionary<string, object>();
        }

        public SolObject(SolObjectWrapper solobj) : this()
        {
            ClassName = solobj.Class;

            foreach (var pair in solobj.Props)
                Properties[pair.Key] = SolHelper.GetAllValues(pair.Value);
        }

        public SolObjectWrapper ToObjectWrapper()
        {
            var obj = new SolObjectWrapper { Class = ClassName };

            foreach (var pair in Properties)
                obj.Props[pair.Key] = SolHelper.GetValueWrapper(pair.Value);

            return obj;
        }
    }
}
