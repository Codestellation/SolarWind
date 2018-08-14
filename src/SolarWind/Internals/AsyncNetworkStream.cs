using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Threading;

namespace Codestellation.SolarWind.Internals
{
    public class AsyncNetworkStream : NetworkStream
    {
        private readonly SocketAsyncEventArgs _receiveArgs;
        private readonly AutoResetValueTaskSource<int> _receiveSource;
        private readonly SocketAsyncEventArgs _sendArgs;
        private readonly AutoResetValueTaskSource<int> _sendSource;

        public AsyncNetworkStream(Socket socket) : base(socket, true)
        {
            _receiveArgs = new SocketAsyncEventArgs();
            _receiveArgs.Completed += OnReceiveCompleted;
            _receiveSource = new AutoResetValueTaskSource<int>();

            _sendArgs = new SocketAsyncEventArgs();
            _sendArgs.Completed += OnSendCompleted;
            _sendSource = new AutoResetValueTaskSource<int>();
        }

        private void OnSendCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                _sendSource.SetResult(e.BytesTransferred);
            }
            else
            {
                _sendSource.SetException(new SocketException((int)e.SocketError));
            }
        }

        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError == SocketError.Success)
            {
                _receiveSource.SetResult(e.BytesTransferred);
            }
            else
            {
                _receiveSource.SetException(new SocketException((int)e.SocketError));
            }
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine();
            //TODO: Check out how this implemented and whether I have to override it

            return base.FlushAsync(cancellationToken);
        }

        //TODO: I can do it with value task but I have to clone ReadAsync method on the SslStream and use both of them. 
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation)
        {
            _receiveArgs.SetBuffer(buffer, offset, count);
            if (Socket.ReceiveAsync(_receiveArgs))
            {
                return _receiveArgs.BytesTransferred;
            }

            return await _receiveSource
                .AwaitValue(cancellation)
                .ConfigureAwait(false);
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var left = count;
            var sent = 0;
            while (left != 0)
            {
                var realOffset = offset + sent;

                _sendArgs.SetBuffer(buffer, realOffset, left);

                if (Socket.SendAsync(_sendArgs))
                {
                    sent += await _sendSource
                        .AwaitValue(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    //Operation has completed synchronously
                    sent += _sendArgs.BytesTransferred;
                }

                left -= sent;
            }
        }
    }
}