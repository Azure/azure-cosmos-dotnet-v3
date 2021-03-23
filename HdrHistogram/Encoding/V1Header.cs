using HdrHistogram.Utilities;

namespace HdrHistogram.Encoding
{
    internal sealed class V1Header : IHeader
    {
        public V1Header(int cookie, ByteBuffer buffer)
        {
            Cookie = cookie;
            PayloadLengthInBytes = buffer.GetInt();
            NormalizingIndexOffset = buffer.GetInt();
            NumberOfSignificantValueDigits = buffer.GetInt();
            LowestTrackableUnitValue = buffer.GetLong();
            HighestTrackableValue = buffer.GetLong();
            IntegerToDoubleValueConversionRatio = buffer.GetDouble();
        }

        public int Cookie { get; }
        public int PayloadLengthInBytes { get; }
        public int NormalizingIndexOffset { get; }
        public int NumberOfSignificantValueDigits { get; }
        public long LowestTrackableUnitValue { get; }
        public long HighestTrackableValue { get; }
        public double IntegerToDoubleValueConversionRatio { get; }
        public int CapacityEstimateExcess => 0;
    }
}