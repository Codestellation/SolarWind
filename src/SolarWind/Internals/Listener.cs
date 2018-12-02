using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind.Internals
{
    internal class Listener : IDisposable
    {
        private readonly SolarWindHubOptions _hubOptions;
        private readonly Socket _listener;
        private bool _disposed;
        private readonly SocketAsyncEventArgs _args;
        private readonly ILogger<Listener> _logger;

        public Listener(SolarWindHubOptions hubOptions)
        {
            _hubOptions = hubOptions;
            _listener = Build.TcpIPv4();
            _args = new SocketAsyncEventArgs();

            _logger = hubOptions.LoggerFactory.CreateLogger<Listener>();
        }

        public void Listen(Uri uri, Action<HubId, Connection> onAccepted)
        {
            IPEndPoint endpoint = uri.ResolveLocalEndpoint();
            _listener.Bind(endpoint);
            _listener.Listen(10);
            _args.Completed += (sender, e) => OnSocketAccepted(e, onAccepted);
            Listen(onAccepted);
        }

        private void Listen(Action<HubId, Connection> onAccepted)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _args.AcceptSocket = null;

                if (!_listener.AcceptAsync(_args))
                {
                    //Completed synchronously
                    OnSocketAccepted(_args, onAccepted);
                }
            }
            catch (Exception e)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError(e, "Failed to start listening");
                }
            }


            //Operation started asynchronously, so exit now and wait for OnAccept callback 
        }

        private async void OnSocketAccepted(SocketAsyncEventArgs e, Action<HubId, Connection> onAccepted)
        {
            SocketError eSocketError = e.SocketError;
            Socket argsAcceptSocket = _args.AcceptSocket;

            //Enter next accept cycle
            Listen(onAccepted);

            if (eSocketError != SocketError.Success)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    _logger.LogError($"Error accepting socket. Exception code = {(int)eSocketError} ({eSocketError})");
                }

                argsAcceptSocket.SafeDispose();
            }
            else
            {
                _logger.LogInformation($"Accepted socket from {e.AcceptSocket.RemoteEndPoint}");
                await Connection
                    .Accept(_hubOptions, argsAcceptSocket, onAccepted)
                    .ConfigureAwait(false);
            }
        }


        public void Dispose()
        {
            _disposed = true;
            _listener.Close();
            _listener.Dispose();
        }
    }
}