using System;
using System.Threading;

namespace Codestellation.SolarWind.Internals
{
    /*
     * Based on ObjectPool<T> from Roslyn source code (with comments reused):
     * https://github.com/dotnet/roslyn/blob/d4dab355b96955aca5b4b0ebf6282575fad78ba8/src/Dependencies/PooledObjects/ObjectPool%601.cs
     */
    // TODO: I bet it works now so well for high contention environments which also tend to hold objects for a relatively long period of time.
    // TODO: It also tends to loose 2nd generation objects occasionally, and that's ugly scenario for long-running applications 
    // TODO: I'm 100% sure it's better to look for other implementations.
    // TODO: My idea is to make it a fast Stack<T> (without excessive checks) under monitor enter/exit. 
    internal class ObjectPool<T> where T : class
    {
        private T _firstItem;
        private readonly T[] _items;
        private readonly Func<T> _generator;

        public ObjectPool(Func<T> generator, int size)
        {
            _generator = generator ?? throw new ArgumentNullException("generator");
            _items = new T[size - 1];
        }

        public T Rent()
        {
            // PERF: Examine the first element. If that fails, RentSlow will look at the remaining elements.
            // Note that the initial read is optimistically not synchronized. That is intentional. 
            // We will interlock only when we have a candidate. in a worst case we may miss some
            // recently returned objects. Not a big deal.
            T inst = _firstItem;
            if (inst == null || inst != Interlocked.CompareExchange(ref _firstItem, null, inst))
            {
                inst = RentSlow();
            }

            return inst;
        }

        public void Return(T item)
        {
            if (_firstItem == null)
            {
                // Intentionally not using interlocked here. 
                // In a worst case scenario two objects may be stored into same slot.
                // It is very unlikely to happen and will only mean that one of the objects will get collected.
                _firstItem = item;
            }
            else
            {
                ReturnSlow(item);
            }
        }

        private T RentSlow()
        {
            for (var i = 0; i < _items.Length; i++)
            {
                // Note that the initial read is optimistically not synchronized. That is intentional. 
                // We will interlock only when we have a candidate. in a worst case we may miss some
                // recently returned objects. Not a big deal.
                T inst = _items[i];
                if (inst != null)
                {
                    if (inst == Interlocked.CompareExchange(ref _items[i], null, inst))
                    {
                        return inst;
                    }
                }
            }

            return _generator();
        }

        private void ReturnSlow(T obj)
        {
            for (var i = 0; i < _items.Length; i++)
            {
                if (_items[i] == null)
                {
                    // Intentionally not using interlocked here. 
                    // In a worst case scenario two objects may be stored into same slot.
                    // It is very unlikely to happen and will only mean that one of the objects will get collected.
                    _items[i] = obj;
                    break;
                }
            }
        }
    }
}