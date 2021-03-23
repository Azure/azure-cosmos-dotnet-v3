using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace HdrHistogram.UnitTests
{
    public abstract class ConcurrentHistogramTestBase : HistogramTestBase
    {
        [Fact]
        public void Can_support_multiple_concurrent_recorders()
        {
            var target = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            const int loopcount = 10 * 1000 * 1000;
            var concurrency = Environment.ProcessorCount;
            var expected = loopcount * concurrency;
            Action foo = () =>
                         {
                             for (var i = 0; i < loopcount; i++)
                                 target.RecordValue(i);
                         };

            var actions = Enumerable.Range(1, concurrency)
                .Select(_ => foo)
                .ToArray();
            Parallel.Invoke(actions);

            Assert.Equal(expected, target.TotalCount);
        }
    }
}