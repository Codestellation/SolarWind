using System;

namespace Codestellation.SolarWind
{
    public readonly struct MessageHeader : IEquatable<MessageHeader>
    {
        public readonly MessageTypeId TypeId;
        public readonly MessageId MessageId;

        public bool IsEmpty => Equals(default);

        public MessageHeader(MessageTypeId typeId,  MessageId messageId)
        {
            TypeId = typeId;
            MessageId = messageId;
        }

        public bool Equals(MessageHeader other) => TypeId.Equals(other.TypeId) && MessageId.Equals(other.MessageId);

        public override bool Equals(object obj) => obj is MessageHeader header && Equals(header);

        public override int GetHashCode()
        {
            unchecked
            {
                return (TypeId.GetHashCode() * 397) ^ MessageId.GetHashCode();
            }
        }

        public static bool operator ==(MessageHeader left, MessageHeader right) => left.Equals(right);

        public static bool operator !=(MessageHeader left, MessageHeader right) => !left.Equals(right);
    }
}