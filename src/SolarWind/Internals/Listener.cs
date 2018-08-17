using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Internals
{
    internal class Listener : IDisposable
    {
        private readonly SolarWindHubOptions _options;
        private readonly Action<HubId, Connection> _onAccepted;
        private readonly Socket _listener;
        private bool _disposed;
        private readonly SocketAsyncEventArgs _args;

        public Listener(SolarWindHubOptions options, Action<HubId, Connection> onAccepted)
        {
            _options = options;
            _onAccepted = onAccepted;
            _listener = Build.TcpIPv4();
            _args = new SocketAsyncEventArgs();
            _args.Completed += OnAccept;
        }

        public void Listen(Uri uri)
        {
            IPEndPoint endpoint = uri.ResolveLocalEndpoint();
            _listener.Bind(endpoint);
            _listener.Listen(10);
            Task.Run(Listen);
        }

        private void Listen()
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
                    OnAccept(null, _args);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            //Operation started asynchronously, so exit now and wait for OnAccept callback 
        }

        private async void OnAccept(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                await Connection
                    .Accept(_options, _args.AcceptSocket, _onAccepted)
                    .ConfigureAwait(false);
            }
            else
            {
                //TODO: Better logging
                Console.WriteLine(e.SocketError);
            }

            //Enter next accept cycle
            Listen();
        }


        public void Dispose()
        {
            _disposed = true;
            _listener.Close();
            _listener.Dispose();
        }
    }
}