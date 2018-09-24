using System;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Codestellation.SolarWind
{
    public class SolarWindHubOptions
    {
        public ILoggerFactory LoggerFactory { get; }

        public X509Certificate Certificate { get; set; }

        /// <summary>
        /// Used to identify application level connections and preserve session between reconnections.
        /// </summary>
        public HubId HubId { get; }

        public SolarWindHubOptions(HubId hubId, ILoggerFactory loggerFactory)
        {
            if (hubId == default)
            {
                throw new ArgumentException("Must not be the default value", nameof(hubId));
            }

            HubId = hubId;
            LoggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        }

        public SolarWindHubOptions(ILoggerFactory loggerFactory)
            : this(HubId.Generate(), loggerFactory)
        {
        }

        internal SolarWindHubOptions Clone() => (SolarWindHubOptions)MemberwiseClone();
    }
}