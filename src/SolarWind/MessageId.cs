using System;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// MessageId is per channel
    /// </summary>
    public struct MessageId : IEquatable<MessageId>
    {
        private readonly ulong _value;
        public static MessageId Empty = default;

        public MessageId(ulong value)
        {
            _value = value;
        }

        /// <inheritdoc />
        public bool Equals(MessageId other) => _value == other._value;

        public override bool Equals(object obj) => obj is MessageId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(MessageId left, MessageId right) => left.Equals(right);

        public static bool operator !=(MessageId left, MessageId right) => !left.Equals(right);

        public MessageId Next() => new MessageId(_value + 1);
    }
}