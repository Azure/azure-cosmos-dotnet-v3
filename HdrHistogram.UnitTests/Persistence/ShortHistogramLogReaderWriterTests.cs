using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace HdrHistogram.UnitTests.Persistence
{
    
    public sealed class ShortHistogramLogReaderWriterTests : HistogramLogReaderWriterTestBase
    {
        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            return new ShortHistogram(highestTrackableValue, numberOfSignificantValueDigits);
        }
        
        [Theory]
        [MemberData(nameof(PowersOfTwo))]
        public void CanRoundTripSingleHistogramsWithFullRangesOfCountsAndValues(long count)
        {
            RoundTripSingleHistogramsWithFullRangesOfCountsAndValues(count);
        }

        public static IEnumerable<object[]> PowersOfTwo()
        {
            return TestCaseGenerator.PowersOfTwo(15)
                .Select(v => new object[1] { v });
        }
    }
}