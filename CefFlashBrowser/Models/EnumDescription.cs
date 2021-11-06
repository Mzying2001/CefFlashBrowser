using System;

namespace CefFlashBrowser.Models
{
    public class EnumDescription<T> where T : Enum
    {
        public T Value { get; set; }
        public string Description { get; set; }

        public EnumDescription(T value, string description)
        {
            Value = value;
            Description = description;
        }
    }
}
