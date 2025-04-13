using CefFlashBrowser.Models;
using CefFlashBrowser.Sol;
using System;
using System.Collections.Generic;

namespace CefFlashBrowser.Utils
{
    public static class SolHelper
    {
        private static Dictionary<Type, string> TypeStringDic { get; } = new Dictionary<Type, string>
        {
            [typeof(int)] = "int",
            [typeof(double)] = "double",
            [typeof(bool)] = "bool",
            [typeof(string)] = "string",
            [typeof(DateTime)] = "DateTime",
            [typeof(SolArray)] = "Array",
            [typeof(SolObject)] = "Object",
            [typeof(byte[])] = "Binary",
            [typeof(SolXml)] = "Xml",
            [typeof(SolXmlDoc)] = "XmlDocument",
            [typeof(SolUndefined)] = "undefined"
        };


        public static Dictionary<string, object> GetAllValues(SolFileWrapper file)
        {
            return GetAllValues(file.Data);
        }

        public static Dictionary<string, object> GetAllValues(IDictionary<string, SolValueWrapper> data)
        {
            var result = new Dictionary<string, object>();

            foreach (var pair in data)
                result[pair.Key] = GetAllValues(pair.Value);

            return result;
        }

        public static object GetAllValues(SolValueWrapper solval)
        {
            object value = solval.GetValue();

            if (value is SolObjectWrapper obj)
            {
                return new SolObject(obj);
            }
            else if (value is SolArrayWrapper arr)
            {
                return new SolArray(arr);
            }
            else
            {
                return value;
            }
        }

        public static void SetAllValues(SolFileWrapper file, IDictionary<string, object> values)
        {
            file.Data.Clear();

            foreach (var pair in values)
                file.Data[pair.Key] = GetValueWrapper(pair.Value);
        }

        public static SolValueWrapper GetValueWrapper(object value)
        {
            var res = new SolValueWrapper();

            if (value is SolObject obj)
            {
                res.SetValue(obj.ToObjectWrapper());
            }
            else if (value is SolArray arr)
            {
                res.SetValue(arr.ToArrayWrapper());
            }
            else
            {
                res.SetValue(value);
            }

            return res;
        }

        public static string GetTypeString(object value)
        {
            if (value == null)
                return "null";

            if (TypeStringDic.TryGetValue(value.GetType(), out var typeStr))
                return typeStr;

            return string.Empty;
        }

        public static object GetDefaultValueOfType(Type type)
        {
            if (type == null) return null;

            if (type == typeof(byte[]))
            {
                return new byte[0];
            }
            else if (type == typeof(DateTime))
            {
                return DateTime.Now;
            }
            else if (type == typeof(SolUndefined))
            {
                return SolUndefined.Value;
            }
            else if (type == typeof(SolXml) || type == typeof(SolXmlDoc))
            {
                return Activator.CreateInstance(type, string.Empty);
            }
            else
            {
                return Activator.CreateInstance(type);
            }
        }
    }
}
