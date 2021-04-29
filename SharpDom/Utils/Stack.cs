using System.Collections.Generic;
using System.Linq;

namespace SharpDom.Data
{
    public class Stack<TData>
    {

        private List<TData> Items { get; }

        public Stack()
        {
            Items = new List<TData>();
        }

        public void Push(TData item)
        {
            Items.Add(item);
        }

        public bool TryPop(out TData item)
        {
            if (Items.Count == 0)
            {
                item = default;
                return false;
            }

            item = Items.Last();
            Items.RemoveAt(Items.Count - 1);
            return true;
        }

        public bool IsEmpty()
        {
            return Items.Count == 0;
        }

    }
}