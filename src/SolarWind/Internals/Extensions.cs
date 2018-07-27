using System.IO;

namespace Codestellation.SolarWind.Internals
{
    public static class Extensions
    {
        public static void SerializeMessage(this ISerializer self, MemoryStream stream, in Message message)
        {
            stream.SetLength(Header.Size);
            stream.Position = Header.Size;

            self.Serialize(message.Payload, stream);

            var header = new Header(message.MessageTypeId, PayloadSize.From(stream, Header.Size));
            byte[] buffer = stream.GetBuffer();
            Header.WriteTo(in header, buffer);
        }
    }
}