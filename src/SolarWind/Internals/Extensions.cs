using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind.Internals
{
    public static class Extensions
    {
        //TODO: Currently it's used for test only. Consider ditching it or moving to the test assembly
        public static void SerializeMessage(this ISerializer self, MemoryStream stream, in MessageHeader header, object data)
        {
            stream.SetLength(WireHeader.Size);
            stream.Position = WireHeader.Size;

            self.Serialize(data, stream);

            var wireHeader = new WireHeader(header, PayloadSize.From(stream, WireHeader.Size));
            byte[] buffer = stream.GetBuffer();
            WireHeader.WriteTo(in wireHeader, buffer);
        }

        public static ValueTask SendHandshake(this AsyncNetworkStream networkStream, HubId hubId)
        {
            //TODO: Use buffer pool one day
            var buffer = new byte[1024];
            //TODO: Check where we have written all the text
            var payloadSize = Encoding.UTF8.GetBytes(hubId.Id, 0, hubId.Id.Length, buffer, WireHeader.Size);
            var msgHeader = new MessageHeader(MessageTypeId.Handshake, MessageId.Empty, MessageId.Empty);
            var outgoingHeader = new WireHeader(msgHeader, new PayloadSize(payloadSize));
            WireHeader.WriteTo(in outgoingHeader, buffer);
            var memory = new ReadOnlyMemory<byte>(buffer, 0, WireHeader.Size + payloadSize);
            return networkStream.WriteAsync(memory, CancellationToken.None);
        }

        public static async ValueTask<HandshakeMessage> ReceiveHandshake(this AsyncNetworkStream self)
        {
            var buffer = new PooledMemoryStream();
            var successful = new SemaphoreSlim(0);

            Task.Run(async () =>
            {
                try
                {
                    if (!await successful.WaitAsync(1000).ConfigureAwait(false))
                    {
                        self.Dispose();
                    }
                }
                catch
                {
                }
                finally
                {
                    successful.Dispose();
                }
            });

            try
            {
                if (!await ReceiveBytesAsync(self, buffer, WireHeader.Size).ConfigureAwait(false))
                {
                    return null;
                }

                buffer.Position = 0;
                WireHeader wireHeader = WireHeader.ReadFrom(buffer);

                buffer.Reset();

                if (!wireHeader.IsHandshake)
                {
                    return null;
                }

                if (!await ReceiveBytesAsync(self, buffer, wireHeader.PayloadSize.Value).ConfigureAwait(false))
                {
                    return null;
                }

                successful.Release();
                return HandshakeMessage.ReadFrom(buffer, wireHeader.PayloadSize.Value);
            }
            finally
            {
                buffer.Dispose();
            }
        }

        private static async ValueTask<bool> ReceiveBytesAsync(this AsyncNetworkStream self, PooledMemoryStream readBuffer, int bytesToReceive)
        {
            var left = bytesToReceive;
            do
            {
                left -= await readBuffer
                    .WriteAsync(self, left, CancellationToken.None)
                    .ConfigureAwait(false);
            } while (left != 0);

            return true;
        }
    }
}