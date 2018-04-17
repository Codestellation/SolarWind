using System;

namespace Codestellation.SolarWind.Threading
{
    internal interface IClientCompletionSource
    {
        void SetGenericResult(object data);
    }
}