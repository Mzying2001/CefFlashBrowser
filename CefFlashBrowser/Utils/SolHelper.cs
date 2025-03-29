using CefFlashBrowser.Models;
using CefFlashBrowser.Sol;
using System.Collections.Generic;

namespace CefFlashBrowser.Utils
{
    public static class SolHelper
    {
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
    }
}
