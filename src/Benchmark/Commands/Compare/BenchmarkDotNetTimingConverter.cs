using System;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Benchmark.Commands.Compare
{
    public class BenchmarkDotNetTimingConverter : ITypeConverter
    {
        public string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData) => throw new NotSupportedException();

        public object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return TimeSpan.MinValue;
            }

            string[] parts = text.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length != 2)
            {
                return TimeSpan.MinValue;
            }

            var value = double.Parse(parts[0]);

            switch (parts[1])
            {
                case "s":
                    return TimeSpan.FromSeconds(value);
                case "ms":
                    return TimeSpan.FromMilliseconds(value);

                default:
                    throw new FormatException($"Unknown time modifier: '{parts[1]}'");
            }
        }
    }
}