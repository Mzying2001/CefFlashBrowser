using CefFlashBrowser.Sol;
using CefFlashBrowser.Utils;
using System.Collections.Generic;

namespace CefFlashBrowser.Models
{
    public class SolArray
    {
        public Dictionary<string, object> AssocPortion { get; }
        public List<object> DensePortion { get; }

        public SolArray()
        {
            AssocPortion = new Dictionary<string, object>();
            DensePortion = new List<object>();
        }

        public SolArray(SolArrayWrapper solarr) : this()
        {
            foreach (var pair in solarr.Assoc)
                AssocPortion[pair.Key] = SolHelper.GetAllValues(pair.Value);

            foreach (var item in solarr.Dense)
                DensePortion.Add(SolHelper.GetAllValues(item));
        }

        public SolArrayWrapper ToArrayWrapper()
        {
            var arr = new SolArrayWrapper();

            foreach (var pair in AssocPortion)
                arr.Assoc[pair.Key] = SolHelper.GetValueWrapper(pair.Value);

            foreach (var item in DensePortion)
                arr.Dense.Add(SolHelper.GetValueWrapper(item));

            return arr;
        }
    }
}
