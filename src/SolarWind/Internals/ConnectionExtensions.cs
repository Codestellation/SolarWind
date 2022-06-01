using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind.Internals
{
    internal static class ConnectionExtensions
    {
        public static async Task<HandshakeMessage> HandshakeAsServer(
            this AsyncNetworkStream stream,
            HubId hubId,
            ILogger logger,
            CancellationToken cancellation)
        {
            logger.LogInformation("Waiting for handshake message from a client");
            HandshakeMessage incoming = await stream
                .ReceiveHandshake(cancellation)
                .ConfigureAwait(false);

            logger.LogInformation($"Received handshake from {incoming.HubId.Id}");

            logger.LogInformation($"Sending server handshake to {incoming.HubId.Id}");
            await stream
                .SendHandshake(hubId, cancellation)
                .ConfigureAwait(false);
            logger.LogInformation($"Handshake sent to {incoming.HubId.Id}");
            return incoming;
        }

        public static async Task<HandshakeMessage> HandshakeAsClient(
            this AsyncNetworkStream stream,
            HubId hubId,
            ILogger logger,
            CancellationToken cancellation)
        {
            logger.LogInformation($"Sending client handshake to {stream.UnderlyingSocket.RemoteEndPoint}");
            await stream
                .SendHandshake(hubId, cancellation)
                .ConfigureAwait(false);

            logger.LogInformation($"Sent client handshake to {stream.UnderlyingSocket.RemoteEndPoint}");
            logger.LogInformation($"Receiving server handshake from {stream.UnderlyingSocket.RemoteEndPoint}");
            HandshakeMessage handshakeResponse = await stream
                .ReceiveHandshake(cancellation)
                .ConfigureAwait(false);

            logger.LogInformation($"Received server handshake from {stream.UnderlyingSocket.RemoteEndPoint}");

            return handshakeResponse;
        }

        
    }
}