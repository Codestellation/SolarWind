using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Codestellation.SolarWind
{
    public delegate ChannelOptions OnChannelAccepted(HubId remoteHubId);

    public class SolarWindHubOptions
    {
        public OnChannelAccepted OnChannelAccepted { get; }
        public X509Certificate Certificate { get; set; }

        /// <summary>
        /// Used to identify application level connections and preserve session between reconnections.
        /// </summary>
        public HubId HubId { get; }

        public SolarWindHubOptions(OnChannelAccepted onChannelAccepted)
        {
            OnChannelAccepted = onChannelAccepted ?? throw new ArgumentNullException(nameof(onChannelAccepted));
            HubId = new HubId($"{Environment.MachineName}:{Process.GetCurrentProcess().ProcessName}");
        }


        public SolarWindHubOptions Clone() => (SolarWindHubOptions)MemberwiseClone();
    }
}