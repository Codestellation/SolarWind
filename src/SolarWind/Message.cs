namespace Codestellation.SolarWind
{
    public readonly struct Message
    {
        public readonly MessageTypeId MessageTypeId;
        public readonly object Payload;

        public Message(MessageTypeId messageTypeId, object payload)
        {
            MessageTypeId = messageTypeId;
            Payload = payload;
        }
    }
}