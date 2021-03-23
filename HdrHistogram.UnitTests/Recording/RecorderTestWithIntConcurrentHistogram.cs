using Xunit;

namespace HdrHistogram.UnitTests.Recording
{
    
    public sealed class RecorderTestWithIntConcurrentHistogram : RecorderTestsBase
    {
        protected override HistogramBase CreateHistogram(long id, long min, long max, int sf)
        {
            //return new IntConcurrentHistogram(id, min, max, sf);
            return HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeWrites()
                .Create();
        }

        protected override Recorder Create(long min, long max, int sf)
        {
            return HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeWrites()
                .WithThreadSafeReads()
                .Create();
        }
    }
}