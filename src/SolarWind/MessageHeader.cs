namespace Codestellation.SolarWind
{
    public readonly struct MessageHeader
    {
        public readonly MessageTypeId TypeId;
        public readonly MessageId MessageId;

        public MessageHeader(MessageTypeId typeId,  MessageId messageId)
        {
            TypeId = typeId;
            MessageId = messageId;
        }
    }
}