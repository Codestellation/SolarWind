using System.IO;
using System.Text;
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

        public static Task SendHandshake(this Stream networkStream, HubId hubId)
        {
            //TODO: Use buffer pool one day
            var buffer = new byte[1024];
            //TODO: Check where we have written all the text
            int payloadSize = Encoding.UTF8.GetBytes(hubId.Id, 0, hubId.Id.Length, buffer, WireHeader.Size);
            var msgHeader = new MessageHeader(MessageTypeId.Handshake, MessageId.Empty);
            var outgoingHeader = new WireHeader(msgHeader, new PayloadSize(payloadSize));
            WireHeader.WriteTo(in outgoingHeader, buffer);
            return networkStream.WriteAsync(buffer, 0, WireHeader.Size + payloadSize);
        }

        public static async Task<HandshakeMessage> ReceiveHandshake(this Stream self)
        {
            PooledMemoryStream buffer = PooledMemoryStream.Rent();
            try
            {
                if (!await ReceiveBytesAsync(self, WireHeader.Size, buffer).ConfigureAwait(false))
                {
                    return null;
                }

                WireHeader wireHeader = WireHeader.ReadFrom(buffer);

                buffer.CompleteRead();
                buffer.Reset();

                if (!wireHeader.IsHandshake)
                {
                    return null;
                }

                if (!await ReceiveBytesAsync(self, wireHeader.PayloadSize.Value, buffer).ConfigureAwait(false))
                {
                    return null;
                }

                return HandshakeMessage.ReadFrom(buffer, wireHeader.PayloadSize.Value);
            }
            finally
            {
                buffer.CompleteRead();
                buffer.CompleteWrite();
                PooledMemoryStream.Return(buffer);
            }
        }

        private static async Task<bool> ReceiveBytesAsync(this Stream self, int bytesToReceive, PooledMemoryStream readBuffer)
        {
            int left = bytesToReceive;
            do
            {
                left -= readBuffer.WriteFrom(self, left);
            } while (left != 0);

            readBuffer.CompleteWrite();

            return true;
        }
    }
}