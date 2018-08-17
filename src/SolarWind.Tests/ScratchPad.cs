using System;
using System.Collections.Concurrent;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests
{
    public class ScratchPad
    {
        [Test]
        public void Test()
        {
            var dict = new ConcurrentDictionary<int, string>();

            var result1 = dict.GetOrAdd(1, i => "first");
            var result2 = dict.GetOrAdd(1, i => "second");

            Console.WriteLine(result1);
            Console.WriteLine(result2);
        }
    }
}