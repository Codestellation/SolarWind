using System;
using Codestellation.SolarWind.Internals;

namespace Codestellation.SolarWind
{
    public readonly struct Message : IDisposable
    {
        public readonly MessageHeader Header;
        public readonly PooledMemoryStream Payload;

        public Message(MessageHeader header, PooledMemoryStream payload)
        {
            Header = header;
            Payload = payload;
        }

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