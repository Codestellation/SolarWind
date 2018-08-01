using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Misc;

namespace Codestellation.SolarWind
{
    public class SolarWindHub : IDisposable
    {
        private readonly SolarWindHubOptions _options;
        private readonly ConcurrentDictionary<ChannelId, Channel> _channels;
        private bool _disposed;
        private readonly Listener _listener;

        public SolarWindHub(SolarWindHubOptions options)
        {
            _options = options.Clone();
            _channels = new ConcurrentDictionary<ChannelId, Channel>();
            _listener = new Listener(_options, (hubId, socket, stream) => OnChannelAccepted(hubId, socket, stream));
        }

        public void Listen(Uri uri) => _listener.Listen(uri);

        public async Task<Channel> Connect(Uri remoteUri)
        {
            Socket socket = Build.TcpIPv4();
            socket.Connect(remoteUri.ResolveRemoteEndpoint());
            var networkStream = new NetworkStream(socket);
            if (remoteUri.UseTls())
            {
                var sslStream = new SslStream(networkStream);
                await sslStream
                    .AuthenticateAsClientAsync(remoteUri.Host)
                    .ConfigureAwait(false);

                if (!sslStream.IsAuthenticated)
                {
                    //TODO: Close stream and say goodbye. 
                }
            }

            await networkStream
                .SendHandshake(_options.HubId)
                .ConfigureAwait(false);

            HandshakeMessage handshakeResponse = await networkStream
                .ReceiveHandshake()
                .ConfigureAwait(false);

            return OnChannelAccepted(handshakeResponse.HubId, socket, networkStream);
        }


        public void Dispose()
        {
            _disposed = true;

            Parallel.ForEach(_channels, c => c.Value.Dispose());
            _listener.Dispose();
        }

        private Channel OnChannelAccepted(HubId remoteHubId, Socket socket, Stream networkStream)
        {
            var channelId = new ChannelId(_options.HubId, remoteHubId);

            if (_channels.TryGetValue(channelId, out Channel channel))
            {
                channel.OnReconnect(socket, networkStream);
            }

            channel = Channel.Server(socket, networkStream, _options);
            if (!_channels.TryAdd(channelId, channel))
            {
                throw new InvalidOperationException("Channel was not open neither reconnected. This should never happen");
            }

            return channel;
        }
    }
}