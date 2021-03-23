using Xunit;

namespace HdrHistogram.UnitTests.Recording
{
    
    public sealed class RecorderTestWithLongHistogram : RecorderTestsBase
    {
        protected override HistogramBase CreateHistogram(long id, long min, long max, int sf)
        {
            //return new LongHistogram(id, min, max, sf);
            return HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .Create();
        }

        protected override Recorder Create(long min, long max, int sf)
        {
            return HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeReads()
                .Create();
        }
    }
}