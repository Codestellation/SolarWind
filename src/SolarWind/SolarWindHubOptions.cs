using System;

namespace Codestellation.SolarWind
{
    public class SolarWindHubOptions
    {
        public ISerializer Serializer { get; set; }
        public SolarWindCallback Callback { get; set; }
        public Action<Channel> OnAccept { get; set; }

        public SolarWindHubOptions Clone() => (SolarWindHubOptions)MemberwiseClone();
    }
}