using System;
using System.Collections.Generic;
using System.Linq;
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

            _listenerThread = new Thread(Listen);
            _listenerThread.Start();
            _listener = Build.TcpIPv4();
        }

        public void Listen(Uri uri)
        {
            IPEndPoint endpoint = ResolveLocalEndpoint(uri);
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

        private IPEndPoint ResolveLocalEndpoint(Uri localUri)
        {
            Uri uri = localUri.Host == "*"
                ? new Uri(localUri.OriginalString.Replace("*", "0.0.0.0"))
                : localUri;
            if (!IPAddress.TryParse(uri.Host, out IPAddress ipAddress))
            {
                ipAddress = Dns
                    .GetHostAddresses(uri.Host)
                    .SingleOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
            }

            if (ipAddress == null)
            {
                throw new ArgumentException($"Could not resolve address {uri}. Please notice IPv6 is not supported currently");
            }

            return new IPEndPoint(ipAddress, uri.Port);
        }

        public void Dispose() => _disposed = true;
    }
}