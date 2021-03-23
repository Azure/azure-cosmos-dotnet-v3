using System.Collections.Generic;
using System.Linq;

namespace HdrHistogram.Persistence
{
    /// <summary>
    /// Provides a method to get the correct <see cref="ICountsDecoder"/> implementation for a given word size.
    /// </summary>
    public static class CountsDecoder
    {
        private static readonly IDictionary<int, ICountsDecoder> Decoders;

        static CountsDecoder()
        {
            Decoders = new ICountsDecoder[]
            {
                new ShortCountsDecoder(),
                new IntCountsDecoder(),
                new LongCountsDecoder(),
                new V2MaxWordSizeCountsDecoder(),
            }.ToDictionary(cd => cd.WordSize);
        }

        /// <summary>
        /// Gets the correct implementation of a <see cref="ICountsDecoder"/> for the supplied word size.
        /// </summary>
        /// <param name="wordSize">The word size of the encoded histogram</param>
        /// <returns>A <see cref="ICountsDecoder"/> implementation.</returns>
        public static ICountsDecoder GetDecoderForWordSize(int wordSize)
        {
            return Decoders[wordSize];
        }
    }
}
