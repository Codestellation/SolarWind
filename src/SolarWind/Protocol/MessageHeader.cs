using System;

namespace Codestellation.SolarWind.Protocol
{
    public readonly struct MessageHeader : IEquatable<MessageHeader>
    {
        public readonly MessageTypeId TypeId;
        public readonly MessageId MessageId;
        public readonly MessageId ReplyTo;

        public bool IsEmpty => Equals(default);

        public MessageHeader(MessageTypeId typeId, MessageId messageId, MessageId replyTo)
        {
            TypeId = typeId;
            MessageId = messageId;
            ReplyTo = replyTo;
        }

        /// <inheritdoc />
        public bool Equals(MessageHeader other) => TypeId.Equals(other.TypeId) && MessageId.Equals(other.MessageId);

        public override bool Equals(object obj) => obj is MessageHeader header && Equals(header);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                return (TypeId.GetHashCode() * 397) ^ MessageId.GetHashCode();
            }
        }

        public static bool operator ==(MessageHeader left, MessageHeader right) => left.Equals(right);

        public static bool operator !=(MessageHeader left, MessageHeader right) => !left.Equals(right);

        /// <inheritdoc />
        public override string ToString() => $"TypeId={TypeId.ToString()}; MsgId={MessageId.ToString()}";
    }
}