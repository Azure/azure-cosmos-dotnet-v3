using Xunit;

namespace HdrHistogram.UnitTests.Recording
{
    
    public sealed class RecorderTestWithShortHistogram : RecorderTestsBase
    {
        protected override HistogramBase CreateHistogram(long id, long min, long max, int sf)
        {
            //return new ShortHistogram(id, min, max, sf);
            return HistogramFactory.With16BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .Create();
        }

        protected override Recorder Create(long min, long max, int sf)
        {
            return HistogramFactory.With16BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeReads()
                .Create();
        }
    }
}