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
        //protected override void Dispose(bool disposing) => Socket.SafeDispose();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellation)
            //ReadAsync(new Memory<byte>(buffer, offset, count), cancellation).AsTask();
            => throw new NotSupportedException();

        //See comments at the top

        public async ValueTask<int> ReadAsync(Memory<byte> to, CancellationToken cancellation)
        {
            if (!MemoryMarshal.TryGetArray(to, out ArraySegment<byte> segment))
            {
                throw new InvalidOperationException("Non array base memory is supported for .net core 2.1+ only");
            }

            try
            {
                if (!TryReceiveSyncNonBlock(segment, out int received))
                {
                    received = await ReceiveAsync(segment).ConfigureAwait(false);
                }

                return received;
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
            {
                throw new IOException("Receive failed", ex);
            }
        }

        private bool TryReceiveSyncNonBlock(in ArraySegment<byte> segment, out int received)
        {
            int available = Socket.Available;
            if (available == 0)
            {
                received = 0;
                return false;
            }

            int bytesToRead = Math.Min(segment.Count, available);
            received = Socket.Receive(segment.Array, segment.Offset, bytesToRead, SocketFlags.None);
            return true;
        }

        private async ValueTask<int> ReceiveAsync(ArraySegment<byte> segment)
        {
            CompletionSourceAsyncEventArgs receiveArgs = SocketEventArgsPool.Instance.Get();
            receiveArgs.SetBuffer(segment.Array, segment.Offset, segment.Count);

            if (Socket.ReceiveAsync(receiveArgs))
            {
                await receiveArgs.Task.ConfigureAwait(false);
            }

            int transferred = receiveArgs.BytesTransferred;

            SocketEventArgsPool.Instance.Return(receiveArgs);


            // Zero transferred bytes means connection has been closed at the counterpart side.
            // See https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socketasynceventargs.bytestransferred?view=netframework-4.7.2
            if (transferred == 0)
            {
                throw BuildConnectionClosedException();
            }

            return transferred;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            //WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();
            => throw new NotSupportedException();


        //See comments at the top
        public async ValueTask WriteAsync(ReadOnlyMemory<byte> from, CancellationToken cancellationToken)
        {
            if (!MemoryMarshal.TryGetArray(from, out ArraySegment<byte> segment))
            {
                throw new InvalidOperationException("Non array base memory is supported for .net core 2.1+ only");
            }

            try
            {
                int left = from.Length;
                var sent = 0;
                while (left != 0)
                {
                    int realOffset = segment.Offset + sent;

                    if (!TrySendSyncNonBlock(ref sent, in segment, realOffset))
                    {
                        sent += await SendAsync(segment, realOffset, left).ConfigureAwait(false);
                    }

                    left = from.Length - sent;
                }
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
            {
                throw new IOException("Send failed", ex);
            }
        }

        private async ValueTask<int> SendAsync(ArraySegment<byte> segment, int realOffset, int left)
        {
            CompletionSourceAsyncEventArgs sendArgs = SocketEventArgsPool.Instance.Get();
            sendArgs.SetBuffer(segment.Array, realOffset, left);

            if (Socket.SendAsync(sendArgs))
            {
                await sendArgs.Task.ConfigureAwait(false);
            }

            //Operation has completed synchronously
            int bytesTransferred = sendArgs.BytesTransferred;
            SocketError socketError = sendArgs.SocketError;

            SocketEventArgsPool.Instance.Return(sendArgs);

            if (socketError == SocketError.Success)
            {
                return bytesTransferred;
            }

            throw BuildIoException(socketError);
        }

        private bool TrySendSyncNonBlock(ref int sent, in ArraySegment<byte> segment, int realOffset)
        {
            try
            {
                if (!Socket.Poll(0, SelectMode.SelectWrite))
                {
                    return false;
                }

                sent += Socket.Send(segment.Array, realOffset, segment.Count, SocketFlags.None);
                return true;
            }
            catch (Exception ex) when (ex is SocketException || ex is ObjectDisposedException)
            {
                throw new IOException("Send failed", ex);
            }
        }

        internal static void HandleAsyncResult(object sender, SocketAsyncEventArgs e)
        {
            var args = (CompletionSourceAsyncEventArgs)e;

            if (args.SocketError != SocketError.Success)
            {
                args.SetException(BuildIoException(args.SocketError));
            }
            else if (args.BytesTransferred == 0)
            {
                // Zero transferred bytes means connection has been closed at the counterpart side.
                // See https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.socketasynceventargs.bytestransferred?view=netframework-4.7.2
                args.SetException(BuildConnectionClosedException());
            }
            else
            {
                args.SetResult();
            }
        }
#else
        public override async ValueTask<int> ReadAsync(Memory<byte> to, CancellationToken cancellation)
        {
            int result = await base.ReadAsync(to, cancellation).ConfigureAwait(false);
            if (result == 0)
            {
                ThrowConnectionClosedException();
            }

            return result;
        }
#endif

        private static IOException ThrowConnectionClosedException() => throw BuildConnectionClosedException();

        private static IOException BuildConnectionClosedException() => BuildIoException(SocketError.SocketError, "The counterpart has closed the connection");

        private static IOException BuildIoException(SocketError socketError, string message = "Send or receive failed")
        {
            var socketException = new SocketException((int)socketError);
            var ioException = new IOException(message, socketException);
            return ioException;
        }
    }
}