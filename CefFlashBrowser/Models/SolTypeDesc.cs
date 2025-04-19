using CefFlashBrowser.Utils;
using System;

namespace CefFlashBrowser.Models
{
    public class SolTypeDesc
    {
        public Type Type { get; set; }
        public string TypeName { get; set; }

        public SolTypeDesc(Type type)
        {
            Type = type;
            TypeName = SolHelper.GetTypeString(type);
        }

        public object CreateInstance()
        {
            return SolHelper.GetDefaultValueOfType(Type);
        }
    }
}
