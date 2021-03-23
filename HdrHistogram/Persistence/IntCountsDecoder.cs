using HdrHistogram.Utilities;

namespace HdrHistogram.Persistence
{
    sealed class IntCountsDecoder : SimpleCountsDecoder
    {
        public override int WordSize => 4;

        protected override long ReadValue(ByteBuffer sourceBuffer)
        {
            return sourceBuffer.GetInt();
        }
    }
}