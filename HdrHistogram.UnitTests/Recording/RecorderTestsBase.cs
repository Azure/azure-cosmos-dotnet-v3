using System;
using System.Threading.Tasks;
using HdrHistogram.Utilities;
using Xunit;

namespace HdrHistogram.UnitTests.Recording
{
    public abstract class RecorderTestsBase
    {
        private const long DefautltLowestDiscernibleValue = 1;
        private const long DefaultHighestTrackableValue = 7716549600;//TimeStamp.Hours(1); // e.g. for 1 hr in system clock ticks (StopWatch.Frequency)
        private const int DefaultSignificantFigures = 3;

        protected abstract HistogramBase CreateHistogram(long id, long min, long max, int sf);
        protected abstract Recorder Create(long min, long max, int sf);

        [Theory]
        [InlineData(0, 1, DefaultSignificantFigures, "lowestTrackableValue", "lowestTrackableValue must be >= 1")]
        [InlineData(1, 1, DefaultSignificantFigures, "highestTrackableValue", "highestTrackableValue must be >= 2 * lowestTrackableValue")]
        [InlineData(1, DefaultHighestTrackableValue, 6, "numberOfSignificantValueDigits", "numberOfSignificantValueDigits must be between 0 and 5")]
        [InlineData(1, DefaultHighestTrackableValue, -1, "numberOfSignificantValueDigits", "numberOfSignificantValueDigits must be between 0 and 5")]
        public void ConstructorShouldRejectInvalidParameters(
           long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits,
           string errorParamName, string errorMessage)
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                Create(lowestTrackableValue,
                    highestTrackableValue,
                    numberOfSignificantValueDigits));
            Assert.Equal(errorParamName, ex.ParamName);
            Assert.StartsWith(errorMessage, ex.Message);
        }

        [Fact]
        public void GetIntervalHistogram_returns_alternating_instances_from_factory()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            var a = recorder.GetIntervalHistogram();
            var b = recorder.GetIntervalHistogram(a);
            var c = recorder.GetIntervalHistogram(b);
            var d = recorder.GetIntervalHistogram(c);

            Assert.NotSame(a, b);
            Assert.Same(a, c);
            Assert.NotSame(a, d);
            Assert.Same(b, d);
        }

        [Fact]
        public void GetIntervalHistogram_returns_current_histogram_values()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValue(1);
            recorder.RecordValue(10);
            recorder.RecordValue(100);
            var histogram = recorder.GetIntervalHistogram();
            Assert.Equal(1, histogram.GetCountAtValue(1));
            Assert.Equal(1, histogram.GetCountAtValue(10));
            Assert.Equal(1, histogram.GetCountAtValue(100));
        }

        [Fact]
        public void GetIntervalHistogram_causes_recording_to_happen_on_new_histogram()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValue(1);
            var histogramPrimary = recorder.GetIntervalHistogram();
            Assert.Equal(1, histogramPrimary.GetCountAtValue(1));

            recorder.RecordValue(10);
            recorder.RecordValue(100);
            var histogramSecondary = recorder.GetIntervalHistogram(histogramPrimary);

            Assert.Equal(0, histogramSecondary.GetCountAtValue(1));
            Assert.Equal(1, histogramSecondary.GetCountAtValue(10));
            Assert.Equal(1, histogramSecondary.GetCountAtValue(100));
        }

        [Fact]
        public void GetIntervalHistogram_resets_recycled_histogram()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValue(1);
            recorder.RecordValue(10);
            recorder.RecordValue(100);
            var histogramPrimary = recorder.GetIntervalHistogram();

            recorder.RecordValue(1);
            recorder.RecordValue(10);
            recorder.RecordValue(100);
            var histogramSecondary = recorder.GetIntervalHistogram(histogramPrimary);

            Assert.Equal(0, histogramPrimary.GetCountAtValue(1));
            Assert.Equal(0, histogramPrimary.GetCountAtValue(10));
            Assert.Equal(0, histogramPrimary.GetCountAtValue(100));
            Assert.Equal(1, histogramSecondary.GetCountAtValue(1));
            Assert.Equal(1, histogramSecondary.GetCountAtValue(10));
            Assert.Equal(1, histogramSecondary.GetCountAtValue(100));
        }

        [Fact]
        public void RecordValue_increments_TotalCount()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValue(1000);
            var histogram = recorder.GetIntervalHistogram();
            Assert.Equal(1, histogram.TotalCount);
        }

        [Fact]
        public void RecordValue_increments_CountAtValue()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValue(1000);
            recorder.RecordValue(1000);
            recorder.RecordValue(1000);
            var histogram = recorder.GetIntervalHistogram();
            Assert.Equal(3, histogram.GetCountAtValue(1000));
        }

        [Fact]
        public void RecordValue_Overflow_ShouldThrowException()
        {
            var highestTrackableValue = DefaultHighestTrackableValue;
            var recorder = Create(DefautltLowestDiscernibleValue, highestTrackableValue, DefaultSignificantFigures);
            Assert.Throws<IndexOutOfRangeException>(() => recorder.RecordValue(highestTrackableValue * 3));
        }

        [Fact]
        public void RecordValueWithCount_increments_TotalCount()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValueWithCount(1000, 10);
            var histogram = recorder.GetIntervalHistogram();
            Assert.Equal(10, histogram.TotalCount);
        }

        [Fact]
        public void RecordValueWithCount_increments_CountAtValue()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValueWithCount(1000, 10);
            recorder.RecordValueWithCount(1000, 10);
            recorder.RecordValueWithCount(5000, 20);
            var histogram = recorder.GetIntervalHistogram();
            Assert.Equal(20, histogram.GetCountAtValue(1000));
            Assert.Equal(20, histogram.GetCountAtValue(5000));
        }

        [Fact]
        public void RecordValueWithCount_Overflow_ShouldThrowException()
        {
            var highestTrackableValue = DefaultHighestTrackableValue;
            var recorder = Create(DefautltLowestDiscernibleValue, highestTrackableValue, DefaultSignificantFigures);
            Assert.Throws<IndexOutOfRangeException>(() => recorder.RecordValueWithCount(highestTrackableValue * 3, 100));
        }

        [Fact]
        public void RecordValueWithExpectedInterval()
        {
            var TestValueLevel = 4L;
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            var valueHistogram = new LongHistogram(DefaultHighestTrackableValue, DefaultSignificantFigures);

            recorder.RecordValueWithExpectedInterval(TestValueLevel, TestValueLevel / 4);
            valueHistogram.RecordValue(TestValueLevel);

            var intervalHistogram = recorder.GetIntervalHistogram();
            // The data will include corrected samples:
            Assert.Equal(1L, intervalHistogram.GetCountAtValue((TestValueLevel * 1) / 4));
            Assert.Equal(1L, intervalHistogram.GetCountAtValue((TestValueLevel * 2) / 4));
            Assert.Equal(1L, intervalHistogram.GetCountAtValue((TestValueLevel * 3) / 4));
            Assert.Equal(1L, intervalHistogram.GetCountAtValue((TestValueLevel * 4) / 4));
            Assert.Equal(4L, intervalHistogram.TotalCount);
            // But the raw data will not:
            Assert.Equal(0L, valueHistogram.GetCountAtValue((TestValueLevel * 1) / 4));
            Assert.Equal(0L, valueHistogram.GetCountAtValue((TestValueLevel * 2) / 4));
            Assert.Equal(0L, valueHistogram.GetCountAtValue((TestValueLevel * 3) / 4));
            Assert.Equal(1L, valueHistogram.GetCountAtValue((TestValueLevel * 4) / 4));
            Assert.Equal(1L, valueHistogram.TotalCount);
        }

        [Fact]
        public void RecordAction_increments_TotalCount()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);

            recorder.Record(() => { });

            var longHistogram = recorder.GetIntervalHistogram();
            Assert.Equal(1, longHistogram.TotalCount);
        }

        [Fact]
        public void Reset_clears_counts_for_instances()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValue(1);
            recorder.RecordValue(10);
            recorder.RecordValue(100);
            var histogramPrimary = recorder.GetIntervalHistogram();

            recorder.RecordValue(1);
            recorder.RecordValue(10);
            recorder.RecordValue(100);

            recorder.Reset();
            var histogramSecondary = recorder.GetIntervalHistogram(histogramPrimary);


            Assert.Equal(0, histogramPrimary.TotalCount);
            Assert.Equal(0, histogramSecondary.TotalCount);
        }

        [Fact]
        public void GetIntervalHistogramInto_copies_data_over_provided_Histogram()
        {
            var originalStart = DateTime.Today.AddDays(-1).MillisecondsSinceUnixEpoch();
            var originalEnd = DateTime.Today.MillisecondsSinceUnixEpoch();
            var targetHistogram = new LongHistogram(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            targetHistogram.StartTimeStamp = originalStart;
            targetHistogram.RecordValue(1);
            targetHistogram.RecordValue(10);
            targetHistogram.RecordValue(100);
            targetHistogram.EndTimeStamp = originalEnd;


            Assert.Equal(3, targetHistogram.TotalCount);
            Assert.Equal(1, targetHistogram.GetCountAtValue(1));
            Assert.Equal(1, targetHistogram.GetCountAtValue(10));
            Assert.Equal(1, targetHistogram.GetCountAtValue(100));

            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValue(1000);
            recorder.RecordValue(10000);
            recorder.RecordValue(100000);

            recorder.GetIntervalHistogramInto(targetHistogram);

            Assert.Equal(3, targetHistogram.TotalCount);
            Assert.Equal(0, targetHistogram.GetCountAtValue(1));
            Assert.Equal(0, targetHistogram.GetCountAtValue(10));
            Assert.Equal(0, targetHistogram.GetCountAtValue(100));
            Assert.Equal(1, targetHistogram.GetCountAtValue(1000));
            Assert.Equal(1, targetHistogram.GetCountAtValue(10000));
            Assert.Equal(1, targetHistogram.GetCountAtValue(100000));
            Assert.NotEqual(originalStart, targetHistogram.StartTimeStamp);
            Assert.NotEqual(originalEnd, targetHistogram.EndTimeStamp);
        }

        [Fact]
        public void Using_external_histogram_for_recycling_throws()
        {
            const int id = -1000;
            var externallyCreatedHistogram = CreateHistogram(id, DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            var recorder = Create(1, DefaultHighestTrackableValue, DefaultSignificantFigures);
            recorder.RecordValue(1000);

            Assert.Throws<InvalidOperationException>(() => recorder.GetIntervalHistogram(externallyCreatedHistogram));

            recorder.GetIntervalHistogramInto(externallyCreatedHistogram);
        }

        [Fact]
        public void RecordScope_increments_TotalCount()
        {
            var recorder = Create(DefautltLowestDiscernibleValue, DefaultHighestTrackableValue, DefaultSignificantFigures);
            using (recorder.RecordScope()) { }
            var histogram = recorder.GetIntervalHistogram();
            Assert.Equal(1, histogram.TotalCount);
        }
    }
}