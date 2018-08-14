using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind.Threading
{
    public class AwaitableQueue<T>
    {
        private readonly SimpleQueue<T> _queue;
        private readonly AutoResetValueTaskSource<T> _source;

        public AwaitableQueue(ContinuationOptions options = ContinuationOptions.None)
        {
            _queue = new SimpleQueue<T>(10);
            _source = new AutoResetValueTaskSource<T>(options);
        }

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

        public ValueTask<T> Await(CancellationToken cancellation)
        {
            lock (_queue)
            {
                return _queue.TryDequeue(out T result) ? new ValueTask<T>(result) : _source.AwaitValue(cancellation);
            }
        }
    }
}