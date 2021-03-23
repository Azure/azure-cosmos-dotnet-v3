using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HdrHistogram.UnitTests.Persistence
{
    
    public sealed class LongConcurrentHistogramLogReaderWriterTests : HistogramLogReaderWriterTestBase
    {
        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            return new LongConcurrentHistogram(1, highestTrackableValue, numberOfSignificantValueDigits);
        }

        [Theory]
        [MemberData(nameof(PowersOfTwo))]
        public void CanRoundTripSingleHistogramsWithFullRangesOfCountsAndValues(long count)
        {
            RoundTripSingleHistogramsWithFullRangesOfCountsAndValues(count);
        }

        public static IEnumerable<object[]> PowersOfTwo()
        {
            return TestCaseGenerator.PowersOfTwo(63)
                .Select(v => new object[1] { v });
        }
    }
}