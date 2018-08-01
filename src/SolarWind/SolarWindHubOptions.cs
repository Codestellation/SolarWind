using System;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace Codestellation.SolarWind
{
    public class SolarWindHubOptions
    {
        private static readonly Action<Channel> DoNothing = delegate { };
        private Action<Channel> _onAccept;
        public ISerializer Serializer { get; set; }
        public SolarWindCallback Callback { get; set; }
        public X509Certificate Certificate { get; set; }

        /// <summary>
        /// Used to identify application level connections and preserve session between reconnections.
        /// </summary>
        public HubId HubId { get; set; }

        public SolarWindHubOptions()
        {
            HubId = new HubId($"{Environment.MachineName}:{Process.GetCurrentProcess().ProcessName}");
        }


        public Action<Channel> OnAccept
        {
            get => _onAccept ?? DoNothing;
            set => _onAccept = value;
        }

        public SolarWindHubOptions Clone() => (SolarWindHubOptions)MemberwiseClone();
    }
}