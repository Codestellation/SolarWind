using System;

namespace Codestellation.SolarWind
{
    /// <summary>
    /// MessageId is a number base structure which is used to distinguish different messages.
    /// It's also helps to implement request/reply messaging pattern in an async manner
    /// </summary>
    public struct MessageId : IEquatable<MessageId>
    {
        private readonly ulong _value;

        /// <summary>
        /// Represents an empty value of <see cref="MessageId" /> structure
        /// </summary>
        public static MessageId Empty = default;

        private MessageId(ulong value)
        {
            _value = value;
        }

        public bool IsEmpty => this == Empty;

        /// <inheritdoc />
        public bool Equals(MessageId other) => _value == other._value;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is MessageId id && Equals(id);

        /// <inheritdoc />
        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(MessageId left, MessageId right) => left.Equals(right);

        public static bool operator !=(MessageId left, MessageId right) => !left.Equals(right);

        /// <summary>
        /// Generates next <see cref="MessageId" /> instance
        /// </summary>
        /// <remarks>This method is not thread safe</remarks>
        /// <returns></returns>
        public MessageId Next() => new MessageId(_value + 1);
    }
}