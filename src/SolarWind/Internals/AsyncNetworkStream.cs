using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Threading;

namespace Codestellation.SolarWind.Internals
{
    // .net core 2.1 has implementation for value-task async read/write methods, however it does work with cancellation tokens differently.
    // so currently they are overridden for compatibility reasons
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


        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation)
            => await ReadAsync(new Memory<byte>(buffer, offset, count), cancellation)
                .ConfigureAwait(false);

        //See comments at the top
        public
#if !NETSTANDARD2_0
        override
#endif
            async ValueTask<int> ReadAsync(Memory<byte> to, CancellationToken cancellation)
        {
            if (!MemoryMarshal.TryGetArray(to, out ArraySegment<byte> segment))
            {
                throw new InvalidOperationException("Non array base memory is supported for .net core 2.1+ only");
            }

            _receiveArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);
            if (Socket.ReceiveAsync(_receiveArgs))
            {
                return await _receiveSource
                    .AwaitValue(cancellation)
                    .ConfigureAwait(false);
            }

            return _receiveArgs.BytesTransferred;
        }

        //See comments at the top
        public
#if !NETSTANDARD2_0
        override
#endif
            async ValueTask WriteAsync(ReadOnlyMemory<byte> from, CancellationToken cancellationToken)
        {
            if (!MemoryMarshal.TryGetArray(from, out ArraySegment<byte> segment))
            {
                throw new InvalidOperationException("Non array base memory is supported for .net core 2.1+ only");
            }

            int left = from.Length;
            var sent = 0;
            while (left != 0)
            {
                int realOffset = segment.Offset + sent;

                _sendArgs.SetBuffer(segment.Array, realOffset, left);


                if (Socket.SendAsync(_sendArgs))
                {
                    sent += await _sendSource
                        .AwaitValue(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    //Operation has completed synchronously
                    if (_sendArgs.SocketError == SocketError.Success)
                    {
                        sent += _sendArgs.BytesTransferred;
                    }
                    else
                    {
                        throw new SocketException((int)_sendArgs.SocketError);
                    }
                }

                left = from.Length - sent;
            }
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            await WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).ConfigureAwait(false);

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
    }
}