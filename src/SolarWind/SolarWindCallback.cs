using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind
{
    public delegate void SolarWindCallback(Channel channel, in MessageHeader header, object data);
}