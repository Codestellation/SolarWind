using System;
using System.IO;
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

        public Socket UnderlyingSocket => Socket;

        public AsyncNetworkStream(Socket socket) : base(socket, true)
        {
            _receiveArgs = new SocketAsyncEventArgs();
            _receiveArgs.Completed += OnReceiveCompleted;
            _receiveSource = new AutoResetValueTaskSource<int>();

            _sendArgs = new SocketAsyncEventArgs();
            _sendArgs.Completed += OnSendCompleted;
            _sendSource = new AutoResetValueTaskSource<int>();

            ReadTimeout = 1000;
            WriteTimeout = 1000;
        }

#if NETSTANDARD2_0
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation)
            => ReadAsync(new Memory<byte>(buffer, offset, count), cancellation).AsTask();

        //See comments at the top

        public async ValueTask<int> ReadAsync(Memory<byte> to, CancellationToken cancellation)
        {
            if (!MemoryMarshal.TryGetArray(to, out ArraySegment<byte> segment))
            {
                throw new InvalidOperationException("Non array base memory is supported for .net core 2.1+ only");
            }

            _receiveArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);
            if (Socket.ReceiveAsync(_receiveArgs))
            {
                //Console.WriteLine("Started async operation");
                return await _receiveSource
                    .AwaitValue(cancellation)
                    .ConfigureAwait(false);
            }

            return _receiveArgs.BytesTransferred;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();

        //See comments at the top
        public async ValueTask WriteAsync(ReadOnlyMemory<byte> from, CancellationToken cancellationToken)
        {
            if (!MemoryMarshal.TryGetArray(from, out ArraySegment<byte> segment))
            {
                throw new InvalidOperationException("Non array base memory is supported for .net core 2.1+ only");
            }

            var left = from.Length;
            var sent = 0;
            while (left != 0)
            {
                var realOffset = segment.Offset + sent;

                _sendArgs.SetBuffer(segment.Array, realOffset, left);

                if (Socket.SendAsync(_sendArgs))
                {
//                    Console.WriteLine($"{Thread.CurrentThread.ManagedThreadId}: Async write");
                    sent += await _sendSource
                        .AwaitValue(cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    //Console.WriteLine($"Sync write {_sendArgs.SocketError}");
                    //Operation has completed synchronously
                    if (_sendArgs.SocketError == SocketError.Success)
                    {
                        sent += _sendArgs.BytesTransferred;
                    }
                    else
                    {
                        SocketError socketError = _sendArgs.SocketError;
                        ThrowException(socketError);
                    }
                }

                left = from.Length - sent;
            }
        }

#endif
        private void OnSendCompleted(object sender, SocketAsyncEventArgs e) => HandleAsyncResult(e, _sendSource);

        private void OnReceiveCompleted(object sender, SocketAsyncEventArgs e) => HandleAsyncResult(e, _receiveSource);

        private static void HandleAsyncResult(SocketAsyncEventArgs e, AutoResetValueTaskSource<int> valueTaskSource)
        {
            if (e.SocketError != SocketError.Success)
            {
                valueTaskSource.SetException(BuildIoException(e.SocketError));
            }
            else if (e.BytesTransferred == 0)
            {
                // Zero transferred bytes means connection has been closed at the counterpart side.
                // See https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socketasynceventargs.bytestransferred?view=netframework-4.7.2
                valueTaskSource.SetException(BuildConnectionClosedException());
            }
            else
            {
                valueTaskSource.SetResult(e.BytesTransferred);
            }
        }

        private static void ThrowException(SocketError socketError) => throw BuildIoException(socketError);


        private static IOException BuildConnectionClosedException() => BuildIoException(SocketError.SocketError, "The counterpart has closed the connection");

        private static IOException BuildIoException(SocketError socketError, string message = "Send or receive failed")
        {
            var socketException = new SocketException((int)socketError);
            var ioException = new IOException(message, socketException);
            return ioException;
        }
    }
}