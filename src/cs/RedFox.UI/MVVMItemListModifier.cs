using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.UI
{
    public class MVVMItemListModifier<T>(MVVMItemList<T> items) : IDisposable
    {
        public MVVMItemList<T> Items { get; set; } = items;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Add(T item)
        {
            Items.Notify = false;
            Items.Add(item);
        }

        public void AddRange(IEnumerable<T> values)
        {
            Items.Notify = false;
            foreach (var item in values)
            {
                Items.Add(item);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Items.Notify = true;
                Items.SendNotify();
            }
        }
    }
}
