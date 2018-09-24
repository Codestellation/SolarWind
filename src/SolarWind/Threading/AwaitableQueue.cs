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
        private readonly AutoResetValueTaskSource<T> _source;

        public AwaitableQueue(ContinuationOptions options = ContinuationOptions.None)
        {
            _queue = new SimpleQueue<T>(10);
            _source = new AutoResetValueTaskSource<T>(options);
        }

        /// <summary>
        /// Puts an instance of T into the <see cref="AwaitableQueue{T}" /> and invokes an awaiter (if any)
        /// </summary>
        /// <param name="value">An instance of <see cref="T" /></param>
        public void Enqueue(in T value)
        {
            lock (_queue)
            {
                if (_source.IsBeingAwaited)
                {
                    _source.SetResult(value);
                }
                else
                {
                    _queue.Enqueue(value);
                }
            }
        }

        /// <summary>
        /// If a value is already available returns it synchronously awaits for one otherwise.
        /// </summary>
        /// <param name="cancellation"></param>
        /// <exception cref="OperationCanceledException">Throws if cancellation was requested</exception>
        /// <returns>Returns an instance of a <see cref="ValueTask{T}" /> which contains the next value in the queue</returns>
        public ValueTask<T> Await(CancellationToken cancellation)
        {
            lock (_queue)
            {
                return _queue.TryDequeue(out T result) ? new ValueTask<T>(result) : _source.AwaitValue(cancellation);
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