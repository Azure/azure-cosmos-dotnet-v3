using Xunit;

namespace HdrHistogram.UnitTests
{
    
    public class LongConcurrentHistogramTests : ConcurrentHistogramTestBase
    {
        protected override int WordSize => sizeof(long);

        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new LongConcurrentHistogram(1, highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With64BitBucketSize()
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .WithThreadSafeWrites()
                .Create();
        }

        protected override HistogramBase Create(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new LongConcurrentHistogram(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(lowestTrackableValue)
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .WithThreadSafeWrites()
                .Create();
        }
    }
}