using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Misc;

namespace Codestellation.SolarWind
{
    public class Listener : IDisposable
    {
        private readonly SolarWindHubOptions _options;
        private readonly Action<HubId, Socket, Stream> _onAccepted;
        private readonly Thread _listenerThread;
        private readonly Socket _listener;
        private bool _disposed;

        public Listener(SolarWindHubOptions options, Action<HubId, Socket, Stream> onAccepted)
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
            //TODO: TLS Authentication
            Stream networkStream = new NetworkStream(socket, true);
            if (_options.Certificate != null)
            {
                //TODO: Try accomplish this async later
                var sslStream = new SslStream(networkStream, false);
                await sslStream
                    .AuthenticateAsServerAsync(_options.Certificate, false, SslProtocols.Tls12, true)
                    .ConfigureAwait(false);

                if (!sslStream.IsAuthenticated)
                {
                    sslStream.Close();
                    return;
                }

                networkStream = sslStream;
            }

            HandshakeMessage incoming = await networkStream
                .ReceiveHandshake()
                .ConfigureAwait(false);

            if (incoming == null)
            {
                networkStream.Close();
                return;
            }

            await networkStream
                .SendHandshake(_options.HubId)
                .ConfigureAwait(false);

            _onAccepted(incoming.HubId, socket, networkStream);
        }

        public void Dispose()
        {
            _disposed = true;
            _listenerThread.Join();
        }
    }
}