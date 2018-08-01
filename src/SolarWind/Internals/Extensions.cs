using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Codestellation.SolarWind.Internals
{
    public static class Extensions
    {
        public static void SerializeMessage(this ISerializer self, MemoryStream stream, in Message message, MessageId messageId)
        {
            stream.SetLength(Header.Size);
            stream.Position = Header.Size;

            self.Serialize(message.Payload, stream);

            var header = new Header(message.MessageTypeId, PayloadSize.From(stream, Header.Size), messageId);
            byte[] buffer = stream.GetBuffer();
            Header.WriteTo(in header, buffer);
        }

        public static Task SendHandshake(this Stream networkStream, HubId hubId)
        {
            //TODO: Use buffer pool one day
            var buffer = new byte[1024];
            //TODO: Check where we have written all the text
            int payloadSize = Encoding.UTF8.GetBytes(hubId.Id, 0, hubId.Id.Length, buffer, Header.Size);

            var outgoingHeader = new Header(MessageTypeId.Handshake, new PayloadSize(payloadSize), default);
            Header.WriteTo(in outgoingHeader, buffer);
            return networkStream.WriteAsync(buffer, 0, Header.Size + payloadSize);
        }

        public static async Task<HandshakeMessage> ReceiveHandshake(this Stream self)
        {
            //TODO: Use buffer pool
            var buffer = new byte[1024];

            if (!await ReceiveBytesAsync(self, Header.Size, buffer).ConfigureAwait(false))
            {
                return null;
            }

            Header header = Header.ReadFrom(buffer);

            if (!header.IsHandshake)
            {
                return null;
            }

            if (!await ReceiveBytesAsync(self, header.PayloadSize.Value, buffer).ConfigureAwait(false))
            {
                return null;
            }

            return HandshakeMessage.ReadFrom(buffer, header.PayloadSize.Value);
        }

        private static async Task<bool> ReceiveBytesAsync(this Stream self, int bytes, byte[] buffer)
        {
            var received = 0;
            //TODO: Handle errors and timeouts. 
            try
            {
                do
                {
                    received += await self
                        .ReadAsync(buffer, received, bytes - received)
                        .ConfigureAwait(false);
                } while (received < bytes);
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }
    }
}