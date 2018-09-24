using System.Threading.Tasks;

namespace Codestellation.SolarWind.Threading
{
    internal class SolarWindCompletionSource<T> : TaskCompletionSource<T>, IClientCompletionSource
    {
        public void SetGenericResult(object data) => TrySetResult((T)data);
    }
}