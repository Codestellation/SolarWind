using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind.Internals
{
    public static class ConnectionExtensions
    {
        public static async Task<HandshakeMessage> HandshakeAsServer(this Connection connection, HubId hubId)
        {
            HandshakeMessage incoming = await connection
                .Stream
                .ReceiveHandshake()
                .ConfigureAwait(false);

            if (incoming == null)
            {
                return null;
            }

            await connection
                .Stream
                .SendHandshake(hubId)
                .ConfigureAwait(false);
            return incoming;
        }

        public static async Task<HandshakeMessage> HandshakeAsClient(this Connection connection, HubId hubId)
        {
            await connection.Stream
                .SendHandshake(hubId)
                .ConfigureAwait(false);

            HandshakeMessage handshakeResponse = await connection
                .Stream
                .ReceiveHandshake()
                .ConfigureAwait(false);

            return handshakeResponse;
        }
    }
}