using System;

namespace Codestellation.SolarWind
{
    public class ChannelOptions
    {
        private static readonly SolarWindCallback EmptyCallback = delegate { };
        public ISerializer Serializer { get; }
        public SolarWindCallback Callback { get; }


        public ChannelOptions(ISerializer serializer) : this(serializer, EmptyCallback)
        {
        }

        public ChannelOptions(ISerializer serializer, SolarWindCallback callback)
        {
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
        }
    }
}