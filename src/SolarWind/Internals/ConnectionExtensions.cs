using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind.Internals
{
    public static class ConnectionExtensions
    {
        internal static async Task<HandshakeMessage> HandshakeAsServer(this AsyncNetworkStream stream, HubId hubId)
        {
            HandshakeMessage incoming = await stream
                .ReceiveHandshake()
                .ConfigureAwait(false);

            if (incoming == null)
            {
                return null;
            }

            await stream
                .SendHandshake(hubId)
                .ConfigureAwait(false);
            return incoming;
        }

        public static async Task<HandshakeMessage> HandshakeAsClient(this AsyncNetworkStream stream, HubId hubId)
        {
            await stream
                .SendHandshake(hubId)
                .ConfigureAwait(false);

            HandshakeMessage handshakeResponse = await stream
                .ReceiveHandshake()
                .ConfigureAwait(false);

            return handshakeResponse;
        }
    }
}