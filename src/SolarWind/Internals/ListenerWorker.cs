using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind.Internals
{
    internal class ListenerWorker : IDisposable
    {
        private readonly IPEndPoint _endpoint;
        private readonly SolarWindHubOptions _options;
        private readonly ILogger _logger;
        private readonly Action<HubId, Connection> _onAccepted;
        private Socket _listener;
        private SocketAsyncEventArgs _args;
        private bool _disposed;
        private ManualResetEventSlim _disposedWaitHandle;

        public ListenerWorker(IPEndPoint endpoint, SolarWindHubOptions options, Action<HubId, Connection> onAccepted)
        {
            _endpoint = endpoint;
            _options = options;
            _onAccepted = onAccepted;
            _logger = options.LoggerFactory.CreateLogger<ListenerWorker>();
        }

        public void Start()
        {
            _listener = _endpoint.BuildTcpSocket();
            _args = new SocketAsyncEventArgs();
            _listener.Bind(_endpoint);
            _listener.Listen(10);
            _args.Completed += (sender, e) => OnSocketAccepted(e, _onAccepted);

            Listen(_onAccepted);
        }

        private void Listen(Action<HubId, Connection> onAccepted)
        {
            if (_disposed)
            {
                _disposedWaitHandle.Set();
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
            Socket argsAcceptSocket = _args.AcceptSocket;
            if (_disposed)
            {
                _args.AcceptSocket.SafeDispose();
                _disposedWaitHandle.Set();
                return;
            }

            SocketError eSocketError = e.SocketError;
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
                _logger.LogInformation($"Accepted socket from {argsAcceptSocket.RemoteEndPoint}");
                await Connection
                    .Accept(_options, argsAcceptSocket, onAccepted)
                    .ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (_listener == null)
            {
                return;
            }

            _disposedWaitHandle = new ManualResetEventSlim(false);
            _disposed = true;
            _listener.Dispose();
            _disposedWaitHandle.Wait(TimeSpan.FromSeconds(1));
        }
    }
}