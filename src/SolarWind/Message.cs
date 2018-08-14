using System;
using Codestellation.SolarWind.Internals;
using Codestellation.SolarWind.Protocol;

namespace Codestellation.SolarWind
{
    public readonly struct Message : IDisposable
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

        public bool IsEmpty => Equals(default);

        public void Dispose()
        {
            if (Payload != null)
            {
                Payload.CompleteWrite();
                Payload.CompleteRead();
                PooledMemoryStream.Return(Payload);
            }
        }
    }
}