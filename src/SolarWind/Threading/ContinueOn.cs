using System.Threading.Tasks;

namespace Codestellation.SolarWind.Threading
{
    internal static class ContinueOn
    {
        // Actually is not true. Continuation will run on a captured context.
        // If the context has TaskScheduler.Current = IOScheduler it will run on it
        // Will skip other cases to avoid dangerous or redundant captures. 
        public static bool IOScheduler => TaskScheduler.Current == IOTaskScheduler.Instance;

        public const bool DefaultScheduler = false;
    }
}