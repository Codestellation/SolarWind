using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Misc;

namespace Codestellation.SolarWind
{
    public class Listener : IDisposable
    {
        private readonly SolarWindHubOptions _options;
        private readonly Action<HubId, Connection> _onAccepted;
        private readonly Thread _listenerThread;
        private readonly Socket _listener;
        private bool _disposed;

        public Listener(SolarWindHubOptions options, Action<HubId, Connection> onAccepted)
        {
            _options = options;
            _onAccepted = onAccepted;
            _listener = Build.TcpIPv4();
            _listenerThread = new Thread(Listen) {Name = "Listener: " + _options.HubId.Id};

            _listenerThread.Start();
        }

        internal void Listen(Uri uri)
        {
            IPEndPoint endpoint = uri.ResolveLocalEndpoint();
            _listener.Bind(endpoint);
            _listener.Listen(10);
        }

        private void Listen()
        {
            while (!_disposed)
            {
                if (!_listener.Poll(50_000, SelectMode.SelectRead))
                {
                    continue;
                }

                Socket socket = _listener.Accept();
                socket.ReceiveTimeout = 10_000;
                Handshake(socket);
            }

            _listener.Close();
            _listener.Dispose();
        }

        private void Handshake(Socket socket) => Task
            .Run(async () => await DoHandshake(socket).ConfigureAwait(false))
            //TODO: Log the errors some how better
            .ContinueWith(task => Console.WriteLine(task.Exception), TaskContinuationOptions.OnlyOnFaulted);

        private async Task DoHandshake(Socket socket)
        {
            Connection connection = await Connection
                .Accept(socket)
                .ConfigureAwait(false);

            HandshakeMessage incoming = await connection
                .HandshakeAsServer(_options.HubId)
                .ConfigureAwait(false);

            _onAccepted(incoming.HubId, connection);
        }

        public void Dispose()
        {
            _disposed = true;
            _listenerThread.Join();
        }
    }
}