using Xunit;

namespace HdrHistogram.UnitTests
{
    
    public class ShortHistogramTests : HistogramTestBase
    {
        protected override int WordSize => sizeof(short);

        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new ShortHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With16BitBucketSize()
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .Create();
        }

        protected override HistogramBase Create(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            //return new ShortHistogram(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits);
            return HistogramFactory.With16BitBucketSize()
                .WithValuesFrom(lowestTrackableValue)
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantValueDigits)
                .Create();
        }
    }
}