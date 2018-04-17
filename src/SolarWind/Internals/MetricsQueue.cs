using System;
using System.Threading;

namespace Codestellation.SolarWind.Internals
{
    internal class ArrayBlockingQueue<T>
    {
        private readonly SimpleQueue<T> _queue;
        private bool _disposed;

        public ArrayBlockingQueue()
        {
            _queue = new SimpleQueue<T>(10);
        }

        public void Enqueue(in T value)
        {
            lock (_queue)
            {
                _queue.Enqueue(value);
                Monitor.PulseAll(_queue);
            }
        }

        public bool TryDequeue(out T result)
        {
            while (!_disposed)
            {
                lock (_queue)
                {
                    if (_queue.TryDequeue(out result))
                    {
                        return true;
                    }

                    Monitor.Wait(_queue, TimeSpan.FromMilliseconds(100));
                }
            }

            result = default;
            return false;
        }

        public int DequeueBatch(T[] batch)
        {
            lock (_queue)
            {
                int itemsToDequeue = Math.Max(batch.Length, _queue.Count);

                for (var i = 0; i < itemsToDequeue; i++)
                {
                    _queue.TryDequeue(out batch[i]);
                }

                return itemsToDequeue;
            }
        }


        public void Dispose(Action<T> dispose)
        {
            lock (_queue)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;

                foreach (T obj in _queue)
                {
                    dispose(obj);
                }

                Monitor.PulseAll(_queue);
            }
        }
    }
}