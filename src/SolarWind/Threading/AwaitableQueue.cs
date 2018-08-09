using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Threading
{
    public class AwaitableQueue<T>
    {
        private readonly Queue<T> _queue;
        private readonly AutoResetValueTaskSource<T> _source;

        public AwaitableQueue()
        {
            _queue = new Queue<T>();
            _source = new AutoResetValueTaskSource<T>();
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
                return _queue.Count == 0 ? _source.AwaitValue(cancellation) : new ValueTask<T>(_queue.Dequeue());
            }
        }
    }
}