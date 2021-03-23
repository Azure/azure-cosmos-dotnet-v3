using HdrHistogram.Encoding;
using HdrHistogram.Utilities;
using Xunit;

namespace HdrHistogram.UnitTests
{
    public abstract class HistogramEncodingTestBase
    {
        protected const long DefautltLowestDiscernibleValue = 1;
        protected const long DefaultHighestTrackableValue = 7716549600;//TimeStamp.Hours(1); // e.g. for 1 hr in system clock ticks (StopWatch.Frequency)
        protected const int DefaultSignificantFigures = 3;

        private static readonly HistogramEncoderV2 EncoderV2 = new Encoding.HistogramEncoderV2();

        [Fact]
        public void Given_a_populated_Histogram_When_encoded_and_decoded_Then_data_is_preserved()
        {
            var source = Create(DefaultHighestTrackableValue, DefaultSignificantFigures);
            Load(source);
            var result = EncodeDecode(source);
            HistogramAssert.AreValueEqual(source, result);
        }

        [Fact]
        public void Given_a_populated_Histogram_When_encoded_and_decoded_with_compression_Then_data_is_preserved()
        {
            var source = Create(DefaultHighestTrackableValue, DefaultSignificantFigures);
            Load(source);
            var result = CompressedEncodeDecode(source);
            HistogramAssert.AreValueEqual(source, result);
        }

        [Fact]
        public void Given_a_Histogram_populated_with_full_range_of_values_When_encoded_and_decoded_Then_data_is_preserved()
        {
            var source = Create(DefaultHighestTrackableValue, DefaultSignificantFigures);
            LoadFullRange(source);
            var result = EncodeDecode(source);
            HistogramAssert.AreValueEqual(source, result);
        }

        [Fact]
        public void Given_a_Histogram_populated_with_full_range_of_values_When_encoded_and_decoded_with_compression_Then_data_is_preserved()
        {
            var source = Create(DefaultHighestTrackableValue, DefaultSignificantFigures);
            LoadFullRange(source);
            var result = CompressedEncodeDecode(source);
            HistogramAssert.AreValueEqual(source, result);
        }

        protected abstract HistogramBase Create(long highestTrackableValue, int numberOfSignificantDigits);

        private static HistogramBase EncodeDecode(HistogramBase source)
        {
            var targetBuffer = ByteBuffer.Allocate(source.GetNeededByteBufferCapacity());
            source.Encode(targetBuffer, EncoderV2);
            targetBuffer.Position = 0;
            return HistogramEncoding.DecodeFromByteBuffer(targetBuffer, 0);
        }

        private static HistogramBase CompressedEncodeDecode(HistogramBase source)
        {
            var targetBuffer = ByteBuffer.Allocate(source.GetNeededByteBufferCapacity());
            source.EncodeIntoCompressedByteBuffer(targetBuffer);
            targetBuffer.Position = 0;
            return HistogramEncoding.DecodeFromCompressedByteBuffer(targetBuffer, 0);
        }

        private static void Load(IRecorder source)
        {
            for (long i = 0L; i < 10000L; i++)
            {
                source.RecordValue(1000L * i);
            }
        }

        protected virtual void LoadFullRange(IRecorder source)
        {
            for (long i = 0L; i < DefaultHighestTrackableValue; i += 100L)
            {
                source.RecordValue(i);
            }
            source.RecordValue(DefaultHighestTrackableValue);
        }
    }
}