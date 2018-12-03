using System;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Threading
{
    internal class SolarWindCompletionSource<T> : TaskCompletionSource<T>, IClientCompletionSource
    {
        public SolarWindCompletionSource()
        {
            IssuedAt = DateTime.UtcNow;
        }

        public DateTime IssuedAt { get; }

        public void SetGenericResult(object data) => TrySetResult((T)data);

        public bool TrySetTimeout(TimeSpan timeout)
        {
            TimeSpan elapsed = DateTime.UtcNow - IssuedAt;
            return timeout < elapsed && TrySetException(new TimeoutException("The request has timeout"));
        }
    }
}