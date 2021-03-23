using Xunit;

namespace HdrHistogram.UnitTests
{
    
    public class HistogramFactoryTests
    {
        #region 16bit recording factory tests

        [Fact]
        public void CanCreateShortHistogram()
        {
            var actual = HistogramFactory.With16BitBucketSize()
                .Create();
            Assert.IsAssignableFrom<ShortHistogram>(actual);
        }

        [Theory]
        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]
        public void CanCreateShortHistogramWithSpecifiedRangeValues(long min, long max, int sf)
        {
            var actual = HistogramFactory.With16BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .Create();
            Assert.IsAssignableFrom<ShortHistogram>(actual);
            Assert.Equal(min, actual.LowestTrackableValue);
            Assert.Equal(max, actual.HighestTrackableValue);
            Assert.Equal(sf, actual.NumberOfSignificantValueDigits);
        }

        [Theory]
        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]
        public void CanCreateShortHistogramRecorder(long min, long max, int sf)
        {
            var actual = HistogramFactory.With16BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeReads()
                .Create();
            var histogram = actual.GetIntervalHistogram();
            Assert.IsAssignableFrom<ShortHistogram>(histogram);
            Assert.Equal(min, histogram.LowestTrackableValue);
            Assert.Equal(max, histogram.HighestTrackableValue);
            Assert.Equal(sf, histogram.NumberOfSignificantValueDigits);
        }

        #endregion

        #region 32bit recording factory tests

        [Fact]
        public void CanCreateIntHistogram()
        {
            var actual = HistogramFactory.With32BitBucketSize()
                .Create();
            Assert.IsAssignableFrom<IntHistogram>(actual);
        }
        [Fact]
        public void CanCreateIntConcurrentHistogram()
        {
            var actual = HistogramFactory.With32BitBucketSize()
                .WithThreadSafeWrites()
                .Create();
            Assert.IsAssignableFrom<IntConcurrentHistogram>(actual);
        }

            [Theory]
        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]

        public void CanCreateIntHistogramWithSpecifiedRangeValues(long min, long max, int sf)
        {
            var actual = HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .Create();
            Assert.IsAssignableFrom<IntHistogram>(actual);
            Assert.Equal(min, actual.LowestTrackableValue);
            Assert.Equal(max, actual.HighestTrackableValue);
            Assert.Equal(sf, actual.NumberOfSignificantValueDigits);
        }
            [Theory]
        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]
        public void IntConcurrentHistogramWithSpecifiedRangeValues(long min, long max, int sf)
        {
            var actual = HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeWrites()

                .Create();
            Assert.IsAssignableFrom<IntConcurrentHistogram>(actual);
            Assert.Equal(min, actual.LowestTrackableValue);

            Assert.Equal(max, actual.HighestTrackableValue);
            Assert.Equal(sf, actual.NumberOfSignificantValueDigits);
        }

[Theory]
        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]
        public void CanCreateIntHistogramRecorder(long min, long max, int sf)

        {
            var actual = HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeReads()
                .Create();
            var histogram = actual.GetIntervalHistogram();
            Assert.IsAssignableFrom<IntHistogram>(histogram);
            Assert.Equal(min, histogram.LowestTrackableValue);
            Assert.Equal(max, histogram.HighestTrackableValue);
            Assert.Equal(sf, histogram.NumberOfSignificantValueDigits);
        }

[Theory]
        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]
        public void CanCreateIntConcurrentHistogramRecorder(long min, long max, int sf)
        {
            var actual = HistogramFactory.With32BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeWrites()
                .WithThreadSafeReads()
                .Create();
            var histogram = actual.GetIntervalHistogram();
            Assert.IsAssignableFrom<IntConcurrentHistogram>(histogram);
            Assert.Equal(min, histogram.LowestTrackableValue);
            Assert.Equal(max, histogram.HighestTrackableValue);
            Assert.Equal(sf, histogram.NumberOfSignificantValueDigits);
        }

        #endregion

        #region 64bit recording factory tests

        [Fact]
        public void CanCreateLongHistogram()
        {
            var actual = HistogramFactory.With64BitBucketSize()
                .Create();
            Assert.IsAssignableFrom<LongHistogram>(actual);
        }
        [Fact]
        public void CanCreateLongConcurrentHistogram()
        {
            var actual = HistogramFactory.With64BitBucketSize()
                .WithThreadSafeWrites()
                .Create();
            Assert.IsAssignableFrom<LongConcurrentHistogram>(actual);
        }

        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]
        public void CanCreateLongHistogramWithSpecifiedRangeValues(long min, long max, int sf)
        {
            var actual = HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .Create();
            Assert.IsAssignableFrom<LongHistogram>(actual);
            Assert.Equal(min, actual.LowestTrackableValue);
            Assert.Equal(max, actual.HighestTrackableValue);
            Assert.Equal(sf, actual.NumberOfSignificantValueDigits);
        }
        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]
        public void LongConcurrentHistogramWithSpecifiedRangeValues(long min, long max, int sf)
        {
            var actual = HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeWrites()
                .Create();
            Assert.IsAssignableFrom<LongConcurrentHistogram>(actual);
            Assert.Equal(min, actual.LowestTrackableValue);
            Assert.Equal(max, actual.HighestTrackableValue);
            Assert.Equal(sf, actual.NumberOfSignificantValueDigits);
        }

        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]
        public void CanCreateLongHistogramRecorder(long min, long max, int sf)
        {
            var actual = HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeReads()
                .Create();
            var histogram = actual.GetIntervalHistogram();
            Assert.IsAssignableFrom<LongHistogram>(histogram);
            Assert.Equal(min, histogram.LowestTrackableValue);
            Assert.Equal(max, histogram.HighestTrackableValue);
            Assert.Equal(sf, histogram.NumberOfSignificantValueDigits);
        }

        [InlineData(1, 5000, 3)]
        [InlineData(1000, 100000, 5)]
        public void CanCreateLongConcurrentHistogramRecorder(long min, long max, int sf)
        {
            var actual = HistogramFactory.With64BitBucketSize()
                .WithValuesFrom(min)
                .WithValuesUpTo(max)
                .WithPrecisionOf(sf)
                .WithThreadSafeWrites()
                .WithThreadSafeReads()
                .Create();
            var histogram = actual.GetIntervalHistogram();
            Assert.IsAssignableFrom<LongConcurrentHistogram>(histogram);
            Assert.Equal(min, histogram.LowestTrackableValue);
            Assert.Equal(max, histogram.HighestTrackableValue);
            Assert.Equal(sf, histogram.NumberOfSignificantValueDigits);
        }

        #endregion
    }
}