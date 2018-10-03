using System;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind.Internals
{
    internal readonly struct Message : IDisposable
    {
        public readonly MessageHeader Header;
        public readonly PooledMemoryStream Payload;

        public Message(MessageHeader header, PooledMemoryStream payload)
        {
            Header = header.IsEmpty
                ? throw new ArgumentException("Must not be a default value", nameof(header))
                : header;
            Payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public void Dispose() => Payload?.Dispose();

        public override string ToString() => Header.ToString();
    }
}