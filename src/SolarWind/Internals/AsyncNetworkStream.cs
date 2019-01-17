using System;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Internals
{
    // .net core 2.1 has implementation for value-task async read/write methods, however it does work with cancellation tokens differently.
    // so currently they are overridden for compatibility reasons
    public class AsyncNetworkStream : NetworkStream
    {
        public Socket UnderlyingSocket => Socket;

        public AsyncNetworkStream(Socket socket) : base(socket, true)
        {
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

            if (Socket.Available > 0)
            {
                int bytesToRead = Math.Min(segment.Count, Socket.Available);
                return Socket.Receive(segment.Array, segment.Offset, bytesToRead, SocketFlags.None);
            }

            var source = new TaskCompletionSource<int>();
            var args = new SocketAsyncEventArgs {UserToken = source};
            args.Completed += HandleAsyncResult;
            args.SetBuffer(segment.Array, segment.Offset, segment.Count);

            if (Socket.ReceiveAsync(args))
            {
                return await source.Task.ConfigureAwait(false);
            }

            args.Completed -= HandleAsyncResult;
            args.UserToken = null;
            args.Dispose();
            return args.BytesTransferred;
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

            int left = from.Length;
            var sent = 0;
            while (left != 0)
            {
                int realOffset = segment.Offset + sent;

                if (Socket.Poll(0, SelectMode.SelectWrite))
                {
                    sent += Socket.Send(segment.Array, realOffset, segment.Count, SocketFlags.None);
                }
                else
                {
                    var source = new TaskCompletionSource<int>();
                    var args = new SocketAsyncEventArgs {UserToken = source};
                    args.Completed += HandleAsyncResult;

                    args.SetBuffer(segment.Array, realOffset, left);

                    if (Socket.SendAsync(args))
                    {
                        sent += await source.Task.ConfigureAwait(false);
                    }
                    else
                    {
                        //Operation has completed synchronously
                        if (args.SocketError == SocketError.Success)
                        {
                            sent += args.BytesTransferred;
                        }
                        else
                        {
                            SocketError socketError = args.SocketError;
                            ThrowException(socketError);
                        }

                        args.Completed -= HandleAsyncResult;
                        args.UserToken = null;
                        args.Dispose();
                    }
                }

                left = from.Length - sent;
            }
        }

#endif

        private static void HandleAsyncResult(object sender, SocketAsyncEventArgs e)
        {
            //Nullify it to prevent double usage during further callbacks
            //TaskCompletionSource<int> copy = source;
            //source = null;
            var source = (TaskCompletionSource<int>)e.UserToken;

            if (e.SocketError != SocketError.Success)
            {
                source.TrySetException(BuildIoException(e.SocketError));
            }
            else if (e.BytesTransferred == 0)
            {
                // Zero transferred bytes means connection has been closed at the counterpart side.
                // See https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socketasynceventargs.bytestransferred?view=netframework-4.7.2
                source.TrySetException(BuildConnectionClosedException());
            }
            else
            {
                source.TrySetResult(e.BytesTransferred);
            }

            e.UserToken = null;
            e.Completed -= HandleAsyncResult;
            e.Dispose();
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