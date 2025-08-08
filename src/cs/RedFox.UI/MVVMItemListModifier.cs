using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RedFox.UI
{
    public class MVVMItemListModifier<T> : IDisposable
    {
        public MVVMItemList<T> Items { get; set; }

        public SynchronizationContext? Context { get; set; }

        public MVVMItemListModifier(MVVMItemList<T> items)
        {
            Items = items;
        }

        public MVVMItemListModifier(MVVMItemList<T> items, SynchronizationContext? context)
        {
            Items = items;
        }

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

                if (Context is not null)
                    Context.Send(x => Items.SendNotify(), null);
                else
                    Items.SendNotify();
            }
        }
    }
}
