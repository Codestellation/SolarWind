using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind
{
    public delegate void SolarWindCallback(Channel channel, MessageHeader header, object data);
}