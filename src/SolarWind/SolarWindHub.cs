using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// Manages channels
    /// </summary>
    public class SolarWindHub : IDisposable
    {
        private readonly SolarWindHubOptions _hubOptions;
        private readonly ConcurrentDictionary<ChannelId, Channel> _channels;
        private readonly Listener _listener;
        private readonly ConcurrentDictionary<Uri, Channel> _outChannels;
        private readonly ConcurrentDictionary<HubId, Channel> _remoteIndex;

        public SolarWindHub(SolarWindHubOptions options)
        {
            _hubOptions = options.Clone();
            _channels = new ConcurrentDictionary<ChannelId, Channel>();
            _outChannels = new ConcurrentDictionary<Uri, Channel>();

            _remoteIndex = new ConcurrentDictionary<HubId, Channel>();
            _listener = new Listener(_hubOptions);
        }

        /// <summary>
        /// Instructs hub to start listening on a port and accept incoming channels.
        /// </summary>
        /// <param name="serverOptions"></param>
        public void Listen(ServerOptions serverOptions)
        {
            EnsureNotDisposed();

            if (serverOptions == null)
            {
                throw new ArgumentNullException(nameof(serverOptions));
            }

            _listener.Listen(serverOptions.Uri, (remote, connection) => OnAccepted(remote, connection, serverOptions.Before, serverOptions.After));
        }

        /// <summary>
        /// Creates a new channel or returns existing channel to the specified.
        /// </summary>
        /// <param name="remoteUri">An uri of the remote solarwind hub</param>
        /// <param name="options">Options for channel creation</param>
        /// <returns>An instance of <see cref="Channel" /></returns>
        /// <remarks>Connection is processed asynchronously thus return channel is usually not connected yet.</remarks>
        public Channel OpenChannelTo(Uri remoteUri, ChannelOptions options)
        {
            EnsureNotDisposed();
            if (remoteUri == null)
            {
                throw new ArgumentNullException(nameof(remoteUri));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            //Someone has already created the channel
            if (_outChannels.TryGetValue(remoteUri, out Channel result))
            {
                return result;
            }

            result = new Channel(options, _hubOptions.ChannelLogger, _hubOptions.SessionLogger) { RemoteUri = remoteUri };
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

        /// <summary>
        /// Looks up a channel by remote <see cref="HubId" />
        /// </summary>
        /// <param name="remoteHubId">An identifier of the remote hub.</param>
        /// <param name="channel">A channel to the remote hub.</param>
        /// <returns>True if hub is found and false otherwise.</returns>
        public bool TryGetChannel(HubId remoteHubId, out Channel channel)
        {
            EnsureNotDisposed();
            return _remoteIndex.TryGetValue(remoteHubId, out channel);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_hubOptions.Cancellation.IsCancellationRequested)
            {
                return;
            }

            _hubOptions.RaiseCancellation();

            _listener.Dispose();
            Parallel.ForEach(_channels, c => c.Value.Dispose());
        }

        public void CloseChannel(Channel channel)
        {
            if (channel == null)
            {
                throw new ArgumentNullException(nameof(channel));
            }

            _channels.TryRemove(channel.ChannelId, out _);
            _remoteIndex.TryRemove(channel.RemoteHubId, out _);
            if (channel.RemoteUri != null)
            {
                _outChannels.TryRemove(channel.RemoteUri, out _);
            }

            channel.Dispose();
        }

        private void OnConnected(Uri remoteUri, HubId remoteHubId, Connection connection)
        {
            Channel channel = _outChannels[remoteUri];
            channel.RemoteHubId = remoteHubId;
            channel.ChannelId = new ChannelId(_hubOptions.HubId, remoteHubId);
            _channels.TryAdd(channel.ChannelId, channel);
            _remoteIndex.TryAdd(remoteHubId, channel);
            channel.OnReconnect(connection);
        }

        private void OnAccepted(HubId remoteHubId, Connection connection, BeforeChannelAccepted before, AfterChannelAccepted after)
        {
            var channelId = new ChannelId(_hubOptions.HubId, remoteHubId);

            Channel channel = _channels.GetOrAdd(channelId, id => new Channel(
                before(id.Remote),
                _hubOptions.ChannelLogger,
                _hubOptions.SessionLogger) { RemoteHubId = remoteHubId });

            channel.ChannelId = channelId;
            _remoteIndex.TryAdd(remoteHubId, channel);
            after(channelId, channel);

            channel.OnReconnect(connection);
        }

        private void EnsureNotDisposed()
        {
            if (_hubOptions.Cancellation.IsCancellationRequested)
            {
                throw new ObjectDisposedException(nameof(SolarWindHub));
            }
        }
    }
}