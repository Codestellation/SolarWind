using System.IO;
using System.Text;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind.Protocol
{
    public class HandshakeMessage
    {
        public HandshakeMessage(HubId hubId)
        {
            HubId = hubId;
        }

        public HubId HubId { get; }

        public byte[] GetBytes() => Encoding.UTF8.GetBytes(HubId.Id);

        public static HandshakeMessage ReadFrom(byte[] buffer, int count)
        {
            var value = Encoding.UTF8.GetString(buffer, 0, count);
            var id = new HubId(value);
            return new HandshakeMessage(id);
        }

        public static HandshakeMessage ReadFrom(PooledMemoryStream buffer, int payloadSizeValue)
        {
            using (var reader = new StreamReader(buffer, Encoding.UTF8, false, 16, true))
            {
                var value = reader.ReadToEnd();
                var id = new HubId(value);
                return new HandshakeMessage(id);
            }
        }
    }
}