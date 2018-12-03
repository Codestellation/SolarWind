using System;

namespace Codestellation.SolarWind.Clients
{
    public class SolarWindClientOptions
    {
        public SolarWindClientOptions()
        {
            RequestTimeout = TimeSpan.FromSeconds(15);
        }

        private TimeSpan _requestTimeout;

        public TimeSpan RequestTimeout
        {
            get => _requestTimeout;
            set => _requestTimeout =
                value < TimeSpan.Zero
                    ? throw new ArgumentException($"Must be greater or equal to {nameof(TimeSpan)}.{nameof(TimeSpan.Zero)}")
                    : value;
        }
    }
}