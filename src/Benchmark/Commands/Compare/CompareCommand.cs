using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using Oakton;

namespace Benchmark.Commands.Compare
{
    public class CompareCommand : OaktonCommand<CompareInput>
    {
        public override bool Execute(CompareInput input)
        {
            if (!TryReadResult(input.Baseline, out IReadOnlyCollection<PingPongBenchmarkResult> baselines))
            {
                return false;
            }

            if (!TryReadResult(input.Current, out IReadOnlyCollection<PingPongBenchmarkResult> current))
            {
                return false;
            }

            var output = new StringBuilder();

            output.AppendLine("## BenchmarkDotNet Results comparison");
            output.AppendLine($"Comparing {input.Current} (current) to {input.Baseline} (base)");
            output.AppendLine("| Method | Runtime | MessageSize | Mean | Error | StdDev | Median | Gen 0 | Gen 1 | Gen 2 | Allocated |");
            output.AppendLine("| ------ | ------- | ----------: | ---: | ----: | -----: | -----: | ----: | ----: | ----: | --------: |");


            foreach (PingPongBenchmarkResult baseline in baselines)
            {
                PingPongBenchmarkResult item = current.Single(x => x.Key == baseline.Key);
                baseline.WriteMarkDownTo(output);
                item.WriteComparedMarkDownTo(baseline, output);
            }


            File.WriteAllText("output.md", output.ToString(), Encoding.UTF8);

            return true;
        }

        private bool TryReadResult(string resultFolder, out IReadOnlyCollection<PingPongBenchmarkResult> results)
        {
            results = null;

            if (!Directory.Exists(resultFolder))
            {
                ConsoleWriter.Write(ConsoleColor.Red, $"Directory '{resultFolder}' not found.");
                return false;
            }

            string[] csvFiles = Directory.GetFiles(resultFolder, "*.csv");

            if (csvFiles == null || csvFiles.Length == 0)
            {
                ConsoleWriter.Write(ConsoleColor.Red, $"Not found any .csv files at '{resultFolder}'.");
                return false;
            }

            if (1 < csvFiles.Length)
            {
                ConsoleWriter.Write(ConsoleColor.Red, $"Ambiguous .csv files at '{resultFolder}': {string.Join(",", csvFiles)}");
                return false;
            }


            using (var reader = new StreamReader(csvFiles[0]))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                csv.Configuration.TypeConverterCache.AddConverter(typeof(TimeSpan), new BenchmarkDotNetTimingConverter());
                results = csv
                    .GetRecords<PingPongBenchmarkResult>()
                    .ToList();
                return true;
            }
        }
    }
}