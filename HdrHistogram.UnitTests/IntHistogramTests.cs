using Xunit;

namespace HdrHistogram.UnitTests
{
    
    public class IntHistogramTests : HistogramTestBase
    {
        protected override int WordSize => sizeof(int);

        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new IntHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With32BitBucketSize()
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .Create();
        }

        protected override HistogramBase Create(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new IntHistogram(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(lowestTrackableValue)
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .Create();
        }
    }
}