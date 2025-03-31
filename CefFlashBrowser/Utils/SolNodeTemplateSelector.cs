using CefFlashBrowser.Models;
using CefFlashBrowser.Sol;
using CefFlashBrowser.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

namespace CefFlashBrowser.Utils
{
    public class SolNodeTemplateSelector : DataTemplateSelector
    {
        public DataTemplate RootTemplate { get; set; }
        public DataTemplate UndefinedTemplate { get; set; }
        public DataTemplate NullTemplate { get; set; }
        public DataTemplate BoolTemplate { get; set; }
        public DataTemplate IntTemplate { get; set; }
        public DataTemplate DoubleTemplate { get; set; }
        public DataTemplate StringTemplate { get; set; }
        public DataTemplate XmlDocTemplate { get; set; }
        public DataTemplate DateTemplate { get; set; }
        public DataTemplate ArrayTemplate { get; set; }
        public DataTemplate ObjectTemplate { get; set; }
        public DataTemplate XmlTemplate { get; set; }
        public DataTemplate BinaryTemplate { get; set; }
        public DataTemplate DefaultTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container)
        {
            var template = GetTemplate(item) ?? DefaultTemplate;
            return template ?? base.SelectTemplate(item, container);
        }

        public DataTemplate GetTemplate(object item)
        {
            if (item is SolNodeViewModel node)
            {
                if (node.Value == null)
                {
                    return NullTemplate;
                }
                else if (node.Value is SolFileWrapper)
                {
                    return RootTemplate;
                }
                else if (node.Value is SolUndefined)
                {
                    return UndefinedTemplate;
                }
                else if (node.Value is bool)
                {
                    return BoolTemplate;
                }
                else if (node.Value is int)
                {
                    return IntTemplate;
                }
                else if (node.Value is double)
                {
                    return DoubleTemplate;
                }
                else if (node.Value is string)
                {
                    return StringTemplate;
                }
                else if (node.Value is SolXmlDoc)
                {
                    return XmlDocTemplate;
                }
                else if (node.Value is DateTime)
                {
                    return DateTemplate;
                }
                else if (node.Value is SolArray)
                {
                    return ArrayTemplate;
                }
                else if (node.Value is SolObject)
                {
                    return ObjectTemplate;
                }
                else if (node.Value is SolXml)
                {
                    return XmlTemplate;
                }
                else if (node.Value is byte[])
                {
                    return BinaryTemplate;
                }
            }
            return null;
        }
    }
}
