using HdrHistogram.Utilities;

namespace HdrHistogram.Persistence
{
    sealed class ShortCountsDecoder : SimpleCountsDecoder
    {
        public override int WordSize => 2;
        protected override long ReadValue(ByteBuffer sourceBuffer)
        {
            return sourceBuffer.GetShort();
        }
    }
}