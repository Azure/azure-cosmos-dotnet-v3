using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HdrHistogram.Utilities;
using Xunit;

namespace HdrHistogram.UnitTests.Persistence
{
    public abstract class HistogramLogReaderWriterTestBase
    {
        private const long DefaultHighestTrackableValue = long.MaxValue - 1;
        private const int DefaultSignificantDigits = 3;
        //Used as the HighestTrackableValue when reading out test files that were generated from the original Java implementation.
        private const long OneHourOfNanoseconds = 3600L * 1000 * 1000 * 1000;

        [Fact]
        public void CanReadEmptyLog()
        {
            byte[] data;
            var startTimeWritten = DateTime.Now;
            var expectedStartTime = startTimeWritten.SecondsSinceUnixEpoch()
                .Round(3);

            using (var writerStream = new MemoryStream())
            {
                HistogramLogWriter.Write(writerStream, startTimeWritten);
                data = writerStream.ToArray();
            }

            using (var readerStream = new MemoryStream(data))
            {
                var reader = new HistogramLogReader(readerStream);
                Assert.Empty(reader.ReadHistograms().ToList());
                var actualStartTime = reader.GetStartTime().SecondsSinceUnixEpoch().Round(3);
                Assert.Equal(expectedStartTime, actualStartTime);
            }
        }

        [Theory]
        [InlineData(3600L * 1000 * 1000, DefaultSignificantDigits, 1000)]
        [InlineData(long.MaxValue / 2, DefaultSignificantDigits, 1000)]
        public void CanRoundTripSingleHistogram(long highestTrackableValue, int significantDigits, int multiplier)
        {
            var histogram = CreatePopulatedHistogram(highestTrackableValue, significantDigits, multiplier);

            histogram.SetTimes();
            var data = histogram.WriteLog();
            var actualHistograms = data.ReadHistograms();

            Assert.Equal(1, actualHistograms.Length);
            HistogramAssert.AreValueEqual(histogram, actualHistograms.Single());
        }

        protected void RoundTripSingleHistogramsWithFullRangesOfCountsAndValues(long count)
        {
            var value = 1;
            var histogram = Create(DefaultHighestTrackableValue, DefaultSignificantDigits);
            histogram.RecordValueWithCount(value, count);

            histogram.SetTimes();
            var data = histogram.WriteLog();
            var actualHistograms = data.ReadHistograms();

            Assert.Equal(1, actualHistograms.Length);
            HistogramAssert.AreValueEqual(histogram, actualHistograms.Single());
        }

        [Theory]
        [InlineData("ATag")]
        [InlineData("AnotherTag")]
        public void CanRoundTripSingleHistogramsWithSparseValues(string tag)
        {
            var histogram = Create(DefaultHighestTrackableValue, DefaultSignificantDigits);
            histogram.Tag = tag;
            histogram.RecordValue(1);
            histogram.RecordValue((long.MaxValue / 2) + 1);

            histogram.SetTimes();
            var data = histogram.WriteLog();
            var actualHistograms = data.ReadHistograms();

            Assert.Equal(1, actualHistograms.Length);
            Assert.Equal(tag, actualHistograms[0].Tag);
            HistogramAssert.AreValueEqual(histogram, actualHistograms.Single());
        }

        [Fact]
        public void CanAppendHistogram()
        {
            var histogram1 = Create(DefaultHighestTrackableValue, DefaultSignificantDigits);
            histogram1.RecordValue(1);
            histogram1.RecordValue((long.MaxValue / 2) + 1);
            histogram1.SetTimes();
            var histogram2 = Create(DefaultHighestTrackableValue, DefaultSignificantDigits);
            histogram2.RecordValue(2);
            histogram2.SetTimes();

            byte[] data;
            using (var writerStream = new MemoryStream())
            using (var log = new HistogramLogWriter(writerStream))
            {
                log.Append(histogram1);
                log.Append(histogram2);
                data = writerStream.ToArray();
            }
            var actualHistograms = data.ReadHistograms();

            Assert.Equal(2, actualHistograms.Length);
            HistogramAssert.AreValueEqual(histogram1, actualHistograms.First());
            HistogramAssert.AreValueEqual(histogram2, actualHistograms.Skip(1).First());
        }

        [Theory]
        [InlineData("tagged-Log.logV2.hlog")]
        public void CanReadV2TaggedLogs(string logPath)
        {
            var readerStream = GetEmbeddedFileStream(logPath);
            var reader = new HistogramLogReader(readerStream);
            int histogramCount = 0;
            long totalCount = 0;
            var accumulatedHistogramWithNoTag = Create(85899345920838, DefaultSignificantDigits);
            var accumulatedHistogramWithTagA = Create(85899345920838, DefaultSignificantDigits);
            foreach (var histogram in reader.ReadHistograms())
            {
                histogramCount++;
                Assert.IsAssignableFrom<HistogramBase>(histogram);// "Expected integer value histograms in log file");

                totalCount += histogram.TotalCount;
                if (string.IsNullOrEmpty(histogram.Tag))
                {
                    accumulatedHistogramWithNoTag.Add(histogram);
                }
                else if (histogram.Tag == "A")
                {
                    accumulatedHistogramWithTagA.Add(histogram);
                }
            }

            Assert.Equal(42, histogramCount);
            Assert.Equal(32290, totalCount);

            HistogramAssert.AreValueEqual(accumulatedHistogramWithNoTag, accumulatedHistogramWithTagA);
        }


        [Theory]
        [InlineData("jHiccup-2.0.7S.logV2.hlog")]
        public void CanReadv2Logs(string logPath)
        {
            var readerStream = GetEmbeddedFileStream(logPath);
            var reader = new HistogramLogReader(readerStream);
            int histogramCount = 0;
            long totalCount = 0;
            var accumulatedHistogram = Create(85899345920838, DefaultSignificantDigits);
            foreach (var histogram in reader.ReadHistograms())
            {
                histogramCount++;
                Assert.IsAssignableFrom<HistogramBase>(histogram);//, "Expected integer value histograms in log file");

                totalCount += histogram.TotalCount;
                accumulatedHistogram.Add(histogram);
            }

            Assert.Equal(62, histogramCount);
            Assert.Equal(48761, totalCount);
            Assert.Equal(1745879039, accumulatedHistogram.GetValueAtPercentile(99.9));
            Assert.Equal(1796210687, accumulatedHistogram.GetMaxValue());
            Assert.Equal(1441812279.474, reader.GetStartTime().SecondsSinceUnixEpoch());
        }

        [Theory]
        [InlineData("jHiccup-2.0.1.logV0.hlog", 0, int.MaxValue, 81, 61256, 1510998015, 1569718271, 1438869961.225)]
        [InlineData("jHiccup-2.0.1.logV0.hlog", 19, 25, 25, 18492, 459007, 623103, 1438869961.225)]
        [InlineData("jHiccup-2.0.1.logV0.hlog", 45, 34, 34, 25439, 1209008127, 1234173951, 1438869961.225)]
        public void CanReadv0Logs(string logPath, int skip, int take,
            int expectedHistogramCount, int expectedCombinedValueCount,
            int expectedCombined999, long expectedCombinedMaxLength,
            double expectedStartTime)
        {
            var readerStream = GetEmbeddedFileStream(logPath);
            var reader = new HistogramLogReader(readerStream);

            int histogramCount = 0;
            long totalCount = 0;
            var accumulatedHistogram = Create(OneHourOfNanoseconds, DefaultSignificantDigits);
            var histograms = ((IHistogramLogV1Reader)reader).ReadHistograms()
                .Skip(skip)
                .Take(take);
            foreach (var histogram in histograms)
            {
                histogramCount++;
                totalCount += histogram.TotalCount;
                accumulatedHistogram.Add(histogram);
            }
            Assert.Equal(expectedHistogramCount, histogramCount);
            Assert.Equal(expectedCombinedValueCount, totalCount);
            Assert.Equal(expectedCombined999, accumulatedHistogram.GetValueAtPercentile(99.9));
            Assert.Equal(expectedCombinedMaxLength, accumulatedHistogram.GetMaxValue());
            Assert.Equal(expectedStartTime, reader.GetStartTime().SecondsSinceUnixEpoch());
        }

        [Theory]
        [InlineData("jHiccup-2.0.6.logV1.hlog", 0, int.MaxValue, 88, 65964, 1829765119, 1888485375, 1438867590.285)]
        [InlineData("jHiccup-2.0.6.logV1.hlog", 5, 15, 15, 11213, 1019740159, 1032323071, 1438867590.285)]
        [InlineData("jHiccup-2.0.6.logV1.hlog", 50, 29, 29, 22630, 1871708159, 1888485375, 1438867590.285)]
        [InlineData("ycsb.logV1.hlog", 0, int.MaxValue, 602, 300056, 1214463, 1546239, 1438613579.295)]
        [InlineData("ycsb.logV1.hlog", 0, 180, 180, 89893, 1375231, 1546239, 1438613579.295)]
        [InlineData("ycsb.logV1.hlog", 180, 520, 422, 210163, 530, 17775, 1438613579.295)]
        public void CanReadv1Logs(string logPath, int skip, int take,
            int expectedHistogramCount, int expectedCombinedValueCount,
            int expectedCombined999, long expectedCombinedMaxLength,
            double expectedStartTime)
        {
            var readerStream = GetEmbeddedFileStream(logPath);
            var reader = new HistogramLogReader(readerStream);
            int histogramCount = 0;
            long totalCount = 0;

            HistogramBase accumulatedHistogram = Create(OneHourOfNanoseconds, DefaultSignificantDigits);
            var histograms = reader.ReadHistograms()
                .Skip(skip)
                .Take(take);
            foreach (var histogram in histograms)
            {
                histogramCount++;
                totalCount += histogram.TotalCount;
                accumulatedHistogram.Add(histogram);
            }

            Assert.Equal(expectedHistogramCount, histogramCount);
            Assert.Equal(expectedCombinedValueCount, totalCount);
            Assert.Equal(expectedCombined999, accumulatedHistogram.GetValueAtPercentile(99.9));
            Assert.Equal(expectedCombinedMaxLength, accumulatedHistogram.GetMaxValue());
            Assert.Equal(expectedStartTime, reader.GetStartTime().SecondsSinceUnixEpoch());
        }

        [Theory]
        [InlineData("ycsb.logV1.hlog", 0, 180, 180, 90033, 1375231, 1546239, 1438613579.295)]
        [InlineData("ycsb.logV1.hlog", 180, 520, 421, 209686, 530, 17775, 1438613579.295)]
        public void CanReadv1Logs_Skip_PreStart(string logPath, int skip, int take,
            int expectedHistogramCount, int expectedCombinedValueCount,
            int expectedCombined999, long expectedCombinedMaxLength,
            double expectedStartTime)
        {
            var readerStream = GetEmbeddedFileStream(logPath);
            var reader = new HistogramLogReader(readerStream);
            int histogramCount = 0;
            long totalCount = 0;

            HistogramBase accumulatedHistogram = Create(OneHourOfNanoseconds, DefaultSignificantDigits);
            var histograms = reader.ReadHistograms()
                .Where(h => h.StartTimeStamp >= reader.GetStartTime().MillisecondsSinceUnixEpoch())
                .Skip(skip)
                .Take(take);
            foreach (var histogram in histograms)
            {
                histogramCount++;
                totalCount += histogram.TotalCount;
                accumulatedHistogram.Add(histogram);
            }

            Assert.Equal(expectedHistogramCount, histogramCount);
            Assert.Equal(expectedCombinedValueCount, totalCount);
            Assert.Equal(expectedCombined999, accumulatedHistogram.GetValueAtPercentile(99.9));
            Assert.Equal(expectedCombinedMaxLength, accumulatedHistogram.GetMaxValue());
            Assert.Equal(expectedStartTime, reader.GetStartTime().SecondsSinceUnixEpoch());
        }

        private HistogramBase CreatePopulatedHistogram(long highestTrackableValue, int significantDigits, int multiplier)
        {
            var histogram = Create(highestTrackableValue, significantDigits);
            //Ensure reasonable number of counts
            long i;
            for (i = 0; i < 10000; i++)
            {
                histogram.RecordValue(i * multiplier);
            }
            //Ensure values recorded across the full range of buckets.
            i = 1;
            do
            {
                histogram.RecordValue(i);
            } while ((i <<= 1) < highestTrackableValue && i > 0);
            return histogram;
        }

        private Stream GetEmbeddedFileStream(string filename)
        {
            var fileName = string.Format(CultureInfo.InvariantCulture, "HdrHistogram.UnitTests.Resources.{0}", filename);
            return GetType().GetTypeInfo()
                .Assembly
                .GetManifestResourceStream(fileName);
        }

        protected abstract HistogramBase Create(long highestTrackableValue, int numberOfSignificantValueDigits);
    }
}