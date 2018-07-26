using System;
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


        public Action<Channel> OnAccept
        {
            get => _onAccept ?? DoNothing;
            set => _onAccept = value;
        }

        public SolarWindHubOptions Clone() => (SolarWindHubOptions)MemberwiseClone();
    }
}