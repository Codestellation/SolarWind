using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Codestellation.SolarWind
{
    public class SolarWindHubOptions
    {
        public X509Certificate Certificate { get; set; }

        /// <summary>
        /// Used to identify application level connections and preserve session between reconnections.
        /// </summary>
        public HubId HubId { get; }

        public SolarWindHubOptions(HubId hubId)
        {
            if (hubId == default)
            {
                throw new ArgumentException("Must not be the default value", nameof(hubId));
            }

            HubId = hubId;
        }

        public SolarWindHubOptions()
        {
            HubId = new HubId($"{Environment.MachineName}:{Process.GetCurrentProcess().ProcessName}");
        }


        public SolarWindHubOptions Clone() => (SolarWindHubOptions)MemberwiseClone();
    }
}