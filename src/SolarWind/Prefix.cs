namespace Codestellation.SolarWind
{
    public struct Prefix
    {
        public long MessageId { get; }
        public long Length { get; }

        public Prefix(long messageId, long length)
        {
            MessageId = messageId;
            Length = length;
        }
    }
}