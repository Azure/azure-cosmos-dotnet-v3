using HdrHistogram.Utilities;

namespace HdrHistogram.Encoding
{
    internal sealed class V0Header : IHeader
    {
        public V0Header(int cookie, ByteBuffer buffer)
        {
            Cookie = cookie;
            NumberOfSignificantValueDigits = buffer.GetInt();
            LowestTrackableUnitValue = buffer.GetLong();
            HighestTrackableValue = buffer.GetLong();
            PayloadLengthInBytes = int.MaxValue;
            IntegerToDoubleValueConversionRatio = 1.0;
            NormalizingIndexOffset = 0;
        }
        public int Cookie { get; }
        public int PayloadLengthInBytes { get; }
        public int NormalizingIndexOffset { get; }
        public int NumberOfSignificantValueDigits { get; }
        public long LowestTrackableUnitValue { get; }
        public long HighestTrackableValue { get; }
        public double IntegerToDoubleValueConversionRatio { get; }
        public int CapacityEstimateExcess => 32;
    }
}