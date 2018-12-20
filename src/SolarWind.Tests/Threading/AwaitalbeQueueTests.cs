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
    [TestFixture(ContinuationOptions.ContinueAsync)]
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
            ValueTask actual = _queue.AwaitEnqueued(CancellationToken.None);

            //assert
            actual.IsCompletedSuccessfully.Should().BeTrue(); //Should be return synchronously

            AssertDequeuedMessage();
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
            ValueTask actual = _queue.AwaitEnqueued(CancellationToken.None);
            Stopwatch sw = Stopwatch.StartNew();
            await actual.ConfigureAwait(false);
            sw.Stop();


            //assert
            Console.WriteLine($"Awaited for {sw.ElapsedMilliseconds:N0} ms.");
            AssertDequeuedMessage();
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
            Assert.ThrowsAsync<TaskCanceledException>(async () => await _queue.AwaitEnqueued(source.Token).ConfigureAwait(false));
        }

        [Test]
        public void Should_not_set_result_for_value_task_multiple_times_during_awaiting_phase()
        {
            ValueTask x = _queue.AwaitEnqueued(CancellationToken.None);

            for (var i = 0; i < 10_000; i++)
            {
                _queue.Enqueue(i);
            }
        }


        [Test]
        public async Task Should_not_lost_elements()
        {
            const int total = 1_000_000;
            await Task.Run(() =>
             {
                 for (var i = 0; i < total; i++)
                 {
                     _queue.Enqueue(i);
                 }
             });

            var expected = 0;

            while (expected < total)
            {
                await _queue.AwaitEnqueued(CancellationToken.None).ConfigureAwait(false);
                _queue.TryDequeue(out int actual).Should().BeTrue();
                actual.Should().Be(expected);
                expected++;
            }
        }

        private void AssertDequeuedMessage()
        {
            _queue.TryDequeue(out int actualValue).Should().BeTrue();
            actualValue.Should().Be(_expected);
        }
    }
}