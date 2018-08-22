using System;
using System.Net;
using System.Net.Sockets;

namespace Codestellation.SolarWind.Internals
{
    internal class Listener : IDisposable
    {
        private readonly HubId _hubId;
        private readonly Socket _listener;
        private bool _disposed;
        private readonly SocketAsyncEventArgs _args;

        public Listener(HubId hubId)
        {
            _hubId = hubId;
            _listener = Build.TcpIPv4();
            _args = new SocketAsyncEventArgs();
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
                Console.WriteLine(e);
            }


            //Operation started asynchronously, so exit now and wait for OnAccept callback 
        }

        private async void OnSocketAccepted(SocketAsyncEventArgs e, Action<HubId, Connection> onAccepted)
        {
            if (e.SocketError == SocketError.Success)
            {
                await Connection
                    .Accept(_hubId, _args.AcceptSocket, onAccepted)
                    .ConfigureAwait(false);
            }
            else
            {
                //TODO: Better logging
                Console.WriteLine(e.SocketError);
            }

            //Enter next accept cycle
            Listen(onAccepted);
        }


        public void Dispose()
        {
            _disposed = true;
            _listener.Close();
            _listener.Dispose();
        }
    }
}