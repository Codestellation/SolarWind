using System.Threading.Tasks;

namespace Codestellation.SolarWind
{
    public class SolarWindCompleteionSource<T> : TaskCompletionSource<T>, ISolarWindCompletionSource
    {
        public void SetGenericResult(object data) => TrySetResult((T)data);
    }
}