using Xunit;

namespace HdrHistogram.UnitTests.Recording
{
    
    public sealed class RecorderTestWithLongConcurrentHistogram : RecorderTestsBase
    {
        protected override HistogramBase CreateHistogram(long id, long min, long max, int sf)
        {
            return HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeWrites()
                .Create();
        }

        protected override Recorder Create(long min, long max, int sf)
        {
            return HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeWrites()
                .WithThreadSafeReads()
                .Create();
        }
    }
}