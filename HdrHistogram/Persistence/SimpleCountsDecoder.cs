using System;
using HdrHistogram.Utilities;

namespace HdrHistogram.Persistence
{
    abstract class SimpleCountsDecoder : ICountsDecoder
    {
        public abstract int WordSize { get; }

        public int ReadCounts(ByteBuffer sourceBuffer, int lengthInBytes, int maxIndex, Action<int, long> setCount)
        {
            var idx = 0;
            int endPosition = sourceBuffer.Position + lengthInBytes;
            while (sourceBuffer.Position < endPosition)
            {
                var item = ReadValue(sourceBuffer);
                setCount(idx++, item);
            }
            return idx;
        }

        protected abstract long ReadValue(ByteBuffer sourceBuffer);
    }
}