using Xunit;

namespace HdrHistogram.UnitTests
{
    
    public sealed class IntHistogramEncodingTests : HistogramEncodingTestBase
    {
        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantDigits)
        {
            //return new IntHistogram(highestTrackableValue, numberOfSignificantDigits);
            return HistogramFactory.With32BitBucketSize()
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantDigits)
                .Create();
        }
    }
}