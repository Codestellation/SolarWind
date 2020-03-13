using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Protocol;
using Codestellation.SolarWind.Threading;

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
            WireHeader.WriteTo(in wireHeader, buffer, 0);
        }

        public static ValueTask SendHandshake(this AsyncNetworkStream networkStream, HubId hubId)
        {
            //TODO: Use buffer pool one day
            var buffer = new byte[1024];
            //TODO: Check where we have written all the text
            var payloadSize = Encoding.UTF8.GetBytes(hubId.Id, 0, hubId.Id.Length, buffer, WireHeader.Size);
            var msgHeader = new MessageHeader(MessageTypeId.Handshake, MessageId.Empty, MessageId.Empty);
            var outgoingHeader = new WireHeader(msgHeader, new PayloadSize(payloadSize));
            WireHeader.WriteTo(in outgoingHeader, buffer, 0);
            var memory = new ReadOnlyMemory<byte>(buffer, 0, WireHeader.Size + payloadSize);
            return networkStream.WriteAsync(memory, CancellationToken.None);
        }

        public static async ValueTask<HandshakeMessage> ReceiveHandshake(this AsyncNetworkStream self)
        {
            Task<HandshakeMessage> handshake = HandshakeMessage(self);
            Task timeout = Task.Delay(2000);
            await Task.WhenAny(handshake, timeout).ConfigureAwait(false);

            if (handshake.IsCompleted)
            {
                return handshake.Result;
            }

            //Cause socket to be close and thus throw IOException from handshake task.
            self.Dispose();
            //return the IOException from the handshake to make it observable by caller
            return await handshake.ConfigureAwait(false);
        }


        private static async Task<HandshakeMessage> HandshakeMessage(this AsyncNetworkStream self)
        {
            PooledMemoryStream buffer = MemoryStreamPool.Instance.Get();
            try
            {
                await ReceiveBytesAsync(self, buffer, WireHeader.Size).ConfigureAwait(ContinueOn.IOScheduler);


                buffer.Position = 0;
                WireHeader wireHeader = WireHeader.ReadFrom(buffer);

                buffer.Reset();

                if (!wireHeader.IsHandshake)
                {
                    throw new SolarWindException("Invalid wire header");
                }

                await ReceiveBytesAsync(self, buffer, wireHeader.PayloadSize.Value).ConfigureAwait(ContinueOn.IOScheduler);
                buffer.Position = 0;
                return Protocol.HandshakeMessage.ReadFrom(buffer, wireHeader.PayloadSize.Value);
            }
            finally
            {
                MemoryStreamPool.Instance.Return(buffer);
            }
        }


        private static async ValueTask ReceiveBytesAsync(this AsyncNetworkStream self, PooledMemoryStream readBuffer, int bytesToReceive)
        {
            var left = bytesToReceive;
            do
            {
                left -= await readBuffer
                    .WriteAsync(self, left, CancellationToken.None)
                    .ConfigureAwait(ContinueOn.IOScheduler);
            } while (left != 0);
        }
    }
}