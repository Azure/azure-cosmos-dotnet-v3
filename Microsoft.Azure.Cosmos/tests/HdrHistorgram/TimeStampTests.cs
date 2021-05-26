using System;
using System.Diagnostics;
using FluentAssertions;
using Xunit;

namespace HdrHistogram.UnitTests
{
    public class TimeStampTests
    {
        [Fact]
        public void TimeStamp_values_are_accurate()
        {
            var delay = TimeSpan.FromSeconds(1);
            var expected = TimeStamp.Seconds(delay.Seconds);
            long minAccepted = (long)(expected * 0.95);
            long maxAccepted = (long)(expected * 1.05);

            var start = Stopwatch.GetTimestamp();
            Spin.Wait(delay);
            var end = Stopwatch.GetTimestamp();
            var actual = end - start;

            actual.Should().BeInRange(minAccepted, maxAccepted);
            Assert.Equal(TimeStamp.Seconds(60), TimeStamp.Minutes(1));
            Assert.Equal(TimeStamp.Minutes(60), TimeStamp.Hours(1));
        }
    }
}