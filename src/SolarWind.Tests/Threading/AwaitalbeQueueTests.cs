using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Codestellation.SolarWind.Threading;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests.Threading
{
    [TestFixture(ContinuationOptions.None)]
    [TestFixture(ContinuationOptions.ForceDefaultTaskScheduler)]
    public class AwaitalbeQueueTests
    {
        private readonly ContinuationOptions _options;
        private AwaitableQueue<int> _queue;
        private int _expected;

        public AwaitalbeQueueTests(ContinuationOptions options)
        {
            _options = options;
        }

        [SetUp]
        public void Setup()
        {
            _queue = new AwaitableQueue<int>(_options);
            _expected = new Random().Next();
        }

        [Test]
        public void Should_return_item_if_its_available()
        {
            //arrange
            _queue.Enqueue(_expected);

            //act
            ValueTask<int> actual = _queue.Await(CancellationToken.None);

            //assert
            actual.IsCompletedSuccessfully.Should().BeTrue();
            actual.Result.Should().Be(_expected);
        }

        [Test]
        public async Task Should_return_item_when_its_available()
        {
            void FillSessionAsync()
            {
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    _queue.Enqueue(_expected);
                });
            }

            //arrange
            FillSessionAsync();

            //act
            ValueTask<int> actual = _queue.Await(CancellationToken.None);
            Stopwatch sw = Stopwatch.StartNew();
            int result = await actual.ConfigureAwait(false);
            sw.Stop();


            //assert
            Console.WriteLine($"Awaited for {sw.ElapsedMilliseconds:N0} ms.");
            result.Should().Be(_expected);
        }


        [Test]
        public void Should_throw_task_cancelled_exception()
        {
            var source = new CancellationTokenSource();

            void FillSessionAsync()
            {
                Task.Run(() =>
                {
                    Thread.Sleep(100);
                    source.Cancel();
                });
            }

            //arrange
            FillSessionAsync();

            //act
            //assert
            Assert.ThrowsAsync<TaskCanceledException>(async () => await _queue.Await(source.Token).ConfigureAwait(false));
        }
    }
}