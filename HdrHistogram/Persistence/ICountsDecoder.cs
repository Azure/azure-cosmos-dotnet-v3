using System;
using HdrHistogram.Utilities;

namespace HdrHistogram.Persistence
{
    /// <summary>
    /// Defines methods to read counts values from a potentially encoded <see cref="ByteBuffer"/>.
    /// </summary>
    public interface ICountsDecoder
    {
        /// <summary>
        /// The target word size for the encoder.
        /// </summary>
        int WordSize { get; }

        /// <summary>
        /// Decodes from a supplied <see cref="ByteBuffer"/> count values and calls a delegate with index and count.
        /// </summary>
        /// <param name="sourceBuffer">The source of the data.</param>
        /// <param name="lengthInBytes">The length in bytes to read.</param>
        /// <param name="maxIndex"></param>
        /// <param name="setCount">A delegate to call with the count for a given index.</param>
        /// <returns>The index that was read to.</returns>
        int ReadCounts(ByteBuffer sourceBuffer, int lengthInBytes, int maxIndex, Action<int, long> setCount);
    }
}