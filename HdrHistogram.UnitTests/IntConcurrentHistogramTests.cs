using Xunit;

namespace HdrHistogram.UnitTests
{
    
    public class IntConcurrentHistogramTests : ConcurrentHistogramTestBase
    {
        protected override int WordSize => sizeof(int);

        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new IntConcurrentHistogram(1, highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With32BitBucketSize()
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .WithThreadSafeWrites()
                .Create();
        }

        protected override HistogramBase Create(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new IntConcurrentHistogram(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(lowestTrackableValue)
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .WithThreadSafeWrites()
                .Create();
        }
    }
}