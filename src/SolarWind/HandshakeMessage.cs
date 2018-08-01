using System.Text;

namespace Codestellation.SolarWind
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
            string value = Encoding.UTF8.GetString(buffer, 0, count);
            var id = new HubId(value);
            return new HandshakeMessage(id);
        }
    }
}