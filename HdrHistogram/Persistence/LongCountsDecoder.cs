using HdrHistogram.Utilities;

namespace HdrHistogram.Persistence
{
    sealed class LongCountsDecoder : SimpleCountsDecoder
    {
        public override int WordSize => 8;
        protected override long ReadValue(ByteBuffer sourceBuffer)
        {
            return sourceBuffer.GetLong();
        }
    }
}