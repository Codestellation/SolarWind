using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind
{
    public class SolarWindHub : IDisposable
    {
        private readonly SolarWindHubOptions _hubOptions;
        private readonly ConcurrentDictionary<ChannelId, Channel> _channels;
        private bool _disposed;
        private readonly Listener _listener;
        private readonly ConcurrentDictionary<Uri, Channel> _outChannels;

        public SolarWindHub(SolarWindHubOptions options)
        {
            _hubOptions = options.Clone();
            _channels = new ConcurrentDictionary<ChannelId, Channel>();
            _outChannels = new ConcurrentDictionary<Uri, Channel>();

            _listener = new Listener(_hubOptions, (hubId, connection) => OnAccepted(hubId, connection));
        }

        public void Listen(Uri uri) => _listener.Listen(uri);

        public Channel OpenChannelTo(Uri remoteUri, ChannelOptions options)
        {
            if (remoteUri == null)
            {
                throw new ArgumentNullException(nameof(remoteUri));
            }

            if (options == null)
            {
                throw new ArgumentNullException();
            }

            //Someone has already created the channel
            if (_outChannels.TryGetValue(remoteUri, out Channel result))
            {
                return result;
            }

            result = new Channel(options);
            Channel added = _outChannels.GetOrAdd(remoteUri, result);
            //Another thread was lucky to add it first. So return his result
            if (added != result)
            {
                return added;
            }

            //Start connection attempts. 
            Connection.ConnectTo(_hubOptions, remoteUri, OnConnected);
            return result;
        }

        public void Dispose()
        {
            _disposed = true;
            Parallel.ForEach(_channels, c => c.Value.Dispose());
            _listener.Dispose();
        }

        private void OnConnected(Uri remoteUri, HubId remoteHubId, Connection connection)
        {
            Channel channel = _outChannels[remoteUri];
            var channelId = new ChannelId(_hubOptions.HubId, remoteHubId);
            _channels.TryAdd(channelId, channel);
            channel.OnReconnect(connection);
        }

        private Channel OnAccepted(HubId remoteHubId, Connection connection)
        {
            var channelId = new ChannelId(_hubOptions.HubId, remoteHubId);

            Channel channel = _channels.GetOrAdd(channelId, id => new Channel(_hubOptions.Before(id.Remote)));
            _hubOptions.After(channelId, channel);

            channel.OnReconnect(connection);
            return channel;
        }
    }
}