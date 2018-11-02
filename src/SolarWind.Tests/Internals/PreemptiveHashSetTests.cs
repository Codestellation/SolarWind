using Codestellation.SolarWind.Internals;
using FluentAssertions;
using NUnit.Framework;

namespace Codestellation.SolarWind.Tests.Internals
{
    [TestFixture]
    public class PreemptiveHashSetTests
    {
        [Test]
        public void Should_save_added_value()
        {
            var hashset = new PreemptiveHashSet<int>(1);
            hashset.Add(1).Should().BeTrue("Element must be already added");
            hashset.Contains(1).Should().BeTrue();
        }

        [Test]
        public void Should_not_save_already_added_value()
        {
            var hashset = new PreemptiveHashSet<int>(1);

            var value = 1;
            hashset.Add(value);

            hashset.Add(value).Should().BeFalse("Element must be already added");
            hashset.Contains(value).Should().BeTrue();
        }

        [Test]
        public void Should_preempt_previous_value_already_added_value()
        {
            var hashset = new PreemptiveHashSet<int>(1);

            var value = 1;
            hashset.Add(value);
            hashset.Add(value + 1);
            hashset.Add(value + 2);

            hashset.Contains(value).Should().BeFalse("Should not contain preempted value");
            hashset.Add(value).Should().BeTrue("Can add the same value if it was preempted earlier");
        }
    }
}