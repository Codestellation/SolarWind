using System;
using System.Text;
using CsvHelper.Configuration.Attributes;

namespace Benchmark.Commands.Compare
{
    public class PingPongBenchmarkResult
    {
        public string Method { get; set; }
        public string Runtime { get; set; }

        public int MessageSize { get; set; }

        public TimeSpan Mean { get; set; }
        public TimeSpan Error { get; set; }
        public TimeSpan StdDev { get; set; }
        public TimeSpan Median { get; set; }

        [Name("Gen 0")]
        public decimal Gen0 { get; set; }

        [Name("Gen 1")]
        public decimal Gen1 { get; set; }

        [Name("Gen 2")]
        public decimal Gen2 { get; set; }

        [TypeConverter(typeof(BenchmarkDotNetAllocatedConverter))]
        public long Allocated { get; set; }

        public (string runtime, int messageSize) Key => (Runtime, MessageSize);

        public void WriteMarkDownTo(StringBuilder output)
            => output.AppendLine(
                $"| {Method} | {Runtime} | {MessageSize} | {GetValue(Mean)} | {GetValue(Error)} | {GetValue(StdDev)} | {GetValue(Median)} | " +
                $"{Gen0:N0} | {Gen1:N0} | {Gen2:N0} | {Allocated:N0} |");

        public void WriteComparedMarkDownTo(PingPongBenchmarkResult baseline, StringBuilder output)
        {
            output.Append("| | | |");

            output.Append($" {GetDiffString(Mean, baseline.Mean)} |");
            output.Append($" {GetDiffString(Error, baseline.Error)} |");
            output.Append($" {GetDiffString(StdDev, baseline.StdDev)} |");
            output.Append($" {GetDiffString(Median, baseline.Median)} |");

            output.Append($" {GetDiffString(Gen0, baseline.Gen0)} |");
            output.Append($" {GetDiffString(Gen1, baseline.Gen1)} |");
            output.Append($" {GetDiffString(Gen2, baseline.Gen2)} |");

            output.AppendLine($" {GetDiffString(Allocated, baseline.Allocated)} |");
        }

        private string GetDiffString(TimeSpan current, TimeSpan baseline) => $"{GetValue(current)} ({GetDiff(current, baseline)}%)";

        private string GetDiffString(decimal current, decimal baseline) => $"{current:N0} ({GetDiff(current, baseline)}%)";

        private string GetDiff(decimal current, decimal baseline)
        {
            if (baseline == 0)
            {
                return "-";
            }

            decimal diff = current - baseline;
            decimal percent = diff / baseline * 100;
            return percent.ToString("N2");
        }

        private string GetValue(TimeSpan value)
        {
            if (1 < value.TotalSeconds)
            {
                return $"{value.TotalSeconds:N3} s";
            }

            return $"{value.TotalMilliseconds:N3} ms";
        }

        public string GetDiff(TimeSpan current, TimeSpan baseline)
        {
            TimeSpan diff = current - baseline;

            double percent = diff.TotalMilliseconds / baseline.TotalMilliseconds * 100;

            return percent.ToString("N2");
        }
    }
}