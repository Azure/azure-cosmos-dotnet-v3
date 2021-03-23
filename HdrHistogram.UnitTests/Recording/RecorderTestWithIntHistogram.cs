using Xunit;

namespace HdrHistogram.UnitTests.Recording
{
    
    public sealed class RecorderTestWithIntHistogram : RecorderTestsBase
    {
        protected override HistogramBase CreateHistogram(long id, long min, long max, int sf)
        {
            //return new IntHistogram(id, min, max, sf);
            return HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .Create();
        }

        protected override Recorder Create(long min, long max, int sf)
        {
            return HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeReads()
                .Create();
        }
    }
}