using System;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind.Threading
{
    /// <summary>
    /// Implements queue data structure which is eligible for allocation free async/await usage
    /// </summary>
    /// <typeparam name="T">Any type </typeparam>
    public class AwaitableQueue<T>
    {
        private readonly SimpleQueue<T> _queue;
        private SemaphoreSlim _lock;

        public AwaitableQueue(ContinuationOptions options = ContinuationOptions.None)
        {
            _queue = new SimpleQueue<T>(10);
            _lock = new SemaphoreSlim(0);
        }

        /// <summary>
        /// Puts an instance of T into the <see cref="AwaitableQueue{T}" /> and invokes an awaiter (if any)
        /// </summary>
        public void Enqueue(in T value)
        {
            lock (_queue)
            {
                _queue.Enqueue(value);
                _lock.Release();
            }
        }

        public bool TryDequeue(out T value)
        {
            lock (_queue)
            {
                if (_queue.TryDequeue(out value))
                {
                    _lock.Wait(0);//Decreases message item counter to avoid false sense of present messages
                    return true;
                }

                return false;
            }

        }

        public int TryDequeueBatch(T[] batch)
        {
            lock (_queue)
            {
                var count = Math.Min(batch.Length, _queue.Count);
                for (var i = 0; i < count; i++)
                {
                    _queue.TryDequeue(out batch[i]);
                }

                if (count > 0)
                {
                    _lock.Release(count);
                }
                return count;
            }
        }

        /// <summary>
        /// If a value is already available returns it synchronously awaits for one otherwise.
        /// </summary>
        /// <param name="cancellation"></param>
        /// <exception cref="OperationCanceledException">Throws if cancellation was requested</exception>
        public ValueTask AwaitEnqueued(CancellationToken cancellation)
        {
            lock (_queue)
            {
                if (_queue.Count > 0)
                {
                    return new ValueTask(Task.CompletedTask);
                }

                if (cancellation.IsCancellationRequested)
                {
                    return new ValueTask(Task.FromCanceled(cancellation));
                }

                return new ValueTask(_lock.WaitAsync(cancellation));
            }
        }

        public void Dispose(Action<T> onDispose)
        {
            if (onDispose == null)
            {
                throw new ArgumentNullException(nameof(onDispose));
            }

            lock (_queue)
            {
                while (_queue.TryDequeue(out T result))
                {
                    onDispose(result);
                }
            }
        }
    }
}