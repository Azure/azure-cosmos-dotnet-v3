using Xunit;

namespace HdrHistogram.UnitTests
{
    
    public sealed class ShortHistogramEncodingTests : HistogramEncodingTestBase
    {
        protected override HistogramBase Create(long highestTrackableValue, int numberOfSignificantDigits)
        {
            //return new ShortHistogram(highestTrackableValue, numberOfSignificantDigits);
            return HistogramFactory.With16BitBucketSize()
                .WithValuesUpTo(highestTrackableValue)
                .WithPrecisionOf(numberOfSignificantDigits)
                .Create();
        }

        protected override void LoadFullRange(IRecorder source)
        {
            for (long i = 0L; i < DefaultHighestTrackableValue; i += 1000L)
            {
                source.RecordValue(i);
            }
            source.RecordValue(DefaultHighestTrackableValue);
        }
    }
}   