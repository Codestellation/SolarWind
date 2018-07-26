using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Codestellation.SolarWind.Misc;

namespace Codestellation.SolarWind
{
    public class SolarWindHub : IDisposable
    {
        private readonly SolarWindHubOptions _options;
        private readonly List<Channel> _channels;
        private readonly Thread _listenerThread;
        private bool _disposed;
        private readonly Socket _listener;

        public SolarWindHub(SolarWindHubOptions options)
        {
            _options = options.Clone();
            _channels = new List<Channel>();

            _listener = Build.TcpIPv4();
            _listenerThread = new Thread(Listen);
            _listenerThread.Start();
        }

        public void Listen(Uri uri)
        {
            IPEndPoint endpoint = uri.ResolveLocalEndpoint();
            _listener.Bind(endpoint);
            _listener.Listen(10);
        }

        private void Listen()
        {
            var localListeners = new List<Socket>();
            while (!_disposed)
            {
                localListeners.Clear();
                localListeners.Add(_listener);

                Socket.Select(localListeners, null, Array.Empty<Socket>(), 100_000);

                foreach (Socket listener in localListeners)
                {
                    Socket socket = listener.Accept();
                    Channel channel = Channel.Server(socket, _options);
                    _channels.Add(channel);
                    _options.OnAccept(channel);
                }
            }
        }

        public Channel Connect(Uri remoteUri)
        {
            Socket socket = Build.TcpIPv4();
            socket.Connect(remoteUri.ResolveRemoteEndpoint());
            Channel channel = Channel.Client(socket, _options);
            _channels.Add(channel);
            return channel;
        }


        public void Dispose() => _disposed = true;
    }
}