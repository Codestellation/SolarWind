using System;

namespace Codestellation.SolarWind
{
    public static class SolarWindHubExtensions
    {
        public static void SendTo(this SolarWindHub hub, HubId remoteHubId, object message, MessageId messageId = default)
        {
            if (hub.TryGetChannel(remoteHubId, out Channel channel))
            {
                channel.Post(message, messageId);
                return;
            }

            throw new InvalidOperationException($"Channel to '{remoteHubId}' was not found.");
        }
    }
}