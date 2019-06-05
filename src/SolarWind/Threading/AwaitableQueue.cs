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
        private readonly ContinuationOptions _options;
        private readonly SimpleQueue<T> _queue;
        private CancellationTokenRegistration _cancellation;
        private volatile TaskCompletionSource<int> _source;

        public AwaitableQueue(ContinuationOptions options = ContinuationOptions.None)
        {
            _options = options;
            _queue = new SimpleQueue<T>(10);
        }

        /// <summary>
        /// Puts an instance of T into the <see cref="AwaitableQueue{T}" /> and invokes an awaiter (if any)
        /// </summary>
        public void Enqueue(in T value)
        {
            lock (_queue)
            {
                _queue.Enqueue(value);

                if (_source != null)
                {
                    _cancellation.Dispose();
                    _source.TrySetResult(0);
                    _source = null;
                }
            }
        }

        public bool TryDequeue(out T value)
        {
            lock (_queue)
            {
                return _queue.TryDequeue(out value);
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

                TaskCreationOptions options = _options == ContinuationOptions.ContinueAsync
                    ? TaskCreationOptions.RunContinuationsAsynchronously
                    : TaskCreationOptions.None;

                _source = new TaskCompletionSource<int>(options);

                _cancellation = cancellation.Register(CancelAwaiter);

                return new ValueTask(_source.Task);
            }
        }

        private void CancelAwaiter()
        {
            lock (_queue)
            {
                if (_source != null && _source.TrySetCanceled())
                {
                    _source = null;
                    _cancellation.Dispose();
                }
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
                CancelAwaiter();
                while (_queue.TryDequeue(out T result))
                {
                    onDispose(result);
                }
            }
        }
    }
}