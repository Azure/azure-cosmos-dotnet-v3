using Xunit;

namespace HdrHistogram.UnitTests
{
    
    public class LongHistogramTests : HistogramTestBase
    {
        protected override int WordSize => sizeof(long);
        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With64BitBucketSize()
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .Create();
        }
        protected override HistogramBase Create(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new LongHistogram(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(lowestTrackableValue)
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .Create();
        }
    }
}
