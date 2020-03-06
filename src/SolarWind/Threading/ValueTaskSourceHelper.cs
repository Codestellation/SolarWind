using System.Diagnostics;

namespace System.Threading.Tasks.Sources
{
    internal static class ValueTaskSourceHelper // separated out of generic to avoid unnecessary duplication
    {
        internal static readonly Action<object> s_sentinel = CompletionSentinel;

        private static void CompletionSentinel(object _) // named method to aid debugging
        {
            const string message = "The sentinel delegate should never be invoked.";
            Debug.Fail(message);
            ThrowInvalidOperationException(message);
        }

        internal static void ThrowInvalidOperationException(string message) => throw new InvalidOperationException(message);
    }
}