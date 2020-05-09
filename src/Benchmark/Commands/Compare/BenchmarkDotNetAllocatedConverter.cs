using System;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Benchmark.Commands.Compare
{
    public class BenchmarkDotNetAllocatedConverter : ITypeConverter
    {
        public string ConvertToString(object value, IWriterRow row, MemberMapData memberMapData)
        {
            var number = (long)value;
            return number.ToString("D B");
        }

        public object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
        {
            if (!text.EndsWith(" B"))
            {
                return null;
            }

            string[] tokens = text.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);
            return long.Parse(tokens[0]);
        }
    }
}