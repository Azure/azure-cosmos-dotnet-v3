using Xunit;

namespace HdrHistogram.UnitTests
{
    
    public sealed class LongHistogramEncodingTests : HistogramEncodingTestBase
    {
        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantDigits)
        {
            //return new LongHistogram(highestTrackableValue, numberOfSignificantDigits);
            return HistogramFactory.With64BitBucketSize()
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantDigits)
                .Create();
        }
    }
}
