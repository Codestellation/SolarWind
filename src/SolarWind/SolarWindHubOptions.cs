using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Codestellation.SolarWind
{
    public class SolarWindHubOptions
    {
        public BeforeChannelAccepted Before { get; }
        public AfterChannelAccepted After { get; }
        public X509Certificate Certificate { get; set; }

        /// <summary>
        /// Used to identify application level connections and preserve session between reconnections.
        /// </summary>
        public HubId HubId { get; }

        public SolarWindHubOptions(BeforeChannelAccepted before, AfterChannelAccepted after)
        {
            Before = before ?? throw new ArgumentNullException(nameof(before));
            After = after ?? throw new ArgumentNullException(nameof(after));
            HubId = new HubId($"{Environment.MachineName}:{Process.GetCurrentProcess().ProcessName}");
        }


        public SolarWindHubOptions Clone() => (SolarWindHubOptions)MemberwiseClone();
    }
}