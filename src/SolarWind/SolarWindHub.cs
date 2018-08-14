using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;

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
            _listener = new Listener(_options, (hubId, connection) => OnChannelAccepted(hubId, connection));
        }

        public void Listen(Uri uri) => _listener.Listen(uri);

        public async Task<Channel> Connect(Uri remoteUri)
        {
            Connection connection = await Connection
                .ConnectTo(remoteUri)
                .ConfigureAwait(false);

            HandshakeMessage handshakeResponse = await connection
                .HandshakeAsClient(_options.HubId)
                .ConfigureAwait(false);

            return OnChannelAccepted(handshakeResponse.HubId, connection);
        }


        public void Dispose()
        {
            _disposed = true;

            Parallel.ForEach(_channels, c => c.Value.Dispose());
            _listener.Dispose();
            
        }

        private Channel OnChannelAccepted(HubId remoteHubId, Connection connection)
        {
            var channelId = new ChannelId(_options.HubId, remoteHubId);
            if (_channels.TryGetValue(channelId, out Channel channel) || _channels.TryAdd(channelId, channel = new Channel(_options)))
            {
                channel.OnReconnect(connection);
                return channel;
            }

            throw new InvalidOperationException("Channel was not open neither reconnected. This should never happen");
        }
    }
}