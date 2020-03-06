using System;
using Microsoft.Extensions.Logging;

namespace Codestellation.SolarWind.Tests
{
    public static class TestContext
    {
        public static readonly ILoggerFactory LoggerFactory = new ConsoleLoggerFactory();

        public class ConsoleLoggerFactory : ILoggerFactory
        {
            public void Dispose() => throw new NotImplementedException();

            public ILogger CreateLogger(string categoryName)
                => ConsoleLogger.Instance;

            public void AddProvider(ILoggerProvider provider) => throw new NotImplementedException();
        }

        public class ConsoleLogger : ILogger
        {
            public static readonly ConsoleLogger Instance = new ConsoleLogger();

            private ConsoleLogger()
            {
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
                => Console.WriteLine(formatter(state, exception));

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable BeginScope<TState>(TState state) => throw new NotImplementedException();
        }
    }
}