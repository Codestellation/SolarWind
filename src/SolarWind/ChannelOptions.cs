using System;

namespace Codestellation.SolarWind
{
    public class ChannelOptions
    {
        public ISerializer Serializer { get; }
        public SolarWindCallback Callback { get; }

        public ChannelOptions(ISerializer serializer, SolarWindCallback callback)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }
    }
}