namespace CefFlashBrowser.Infrastructure.Collections
{
    public sealed class ListReorderRequest
    {
        public ListReorderRequest(object item, object targetItem, bool insertAfter)
        {
            Item = item;
            TargetItem = targetItem;
            InsertAfter = insertAfter;
        }

        public object Item { get; }
        public object TargetItem { get; }
        public bool InsertAfter { get; }
    }
}
