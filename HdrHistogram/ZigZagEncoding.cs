using HdrHistogram.Utilities;

namespace HdrHistogram
{
    /// <summary>
    /// Exposes methods to write values to a <see cref="ByteBuffer"/> with ZigZag LEB128-64b9B-variant encoding.
    /// (Little Endian Base128 Encoding, 64bit value store as a maximum of 9Bytes)
    /// </summary>
    /// <remarks>
    /// <p>
    /// This class provides encoding and decoding methods for writing and reading ZigZag-encoded LEB128-64b9B-variant(Little Endian Base 128) values to/from a <see cref="ByteBuffer"/>.
    /// LEB128's variable length encoding provides for using a smaller number of bytes for smaller values, and the use of ZigZag encoding allows small(closer to zero) negative values to use fewer bytes.
    /// Details on both LEB128 and ZigZag can be readily found elsewhere.
    /// </p>
    /// <p>
    /// The LEB128-64b9B-variant encoding used here diverges from the "original" LEB128 as it extends to 64 bit values.
    /// In the original LEB128, a 64 bit value can take up to 10 bytes in the stream, where this variant's encoding of a 64 bit values will max out at 9 bytes.
    /// As such, this encoder/decoder should NOT be used for encoding or decoding "standard" LEB128 formats (e.g.Google Protocol Buffers).
    /// </p>
    /// <p>
    /// ZigZag Encoding explained here - https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
    /// LEB128 explained here - https://en.wikipedia.org/wiki/LEB128
    /// </p>
    /// </remarks>
    public static class ZigZagEncoding
    {
        /// <summary>
        /// Writes a 64 bit integer (<see cref="long"/>) value to the given buffer in LEB128-64b9B-variant ZigZag encoded format.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="value">The value to write to the buffer.</param>
        public static void PutLong(ByteBuffer buffer, long value)
        {
            value = (value << 1) ^ (value >> 63);
            if (value >> 7 == 0)
            {
                buffer.Put((byte)value);
            }
            else {
                buffer.Put((byte)((value & 0x7F) | 0x80));
                if (value >> 14 == 0)
                {
                    buffer.Put((byte)(value >> 7));
                }
                else {
                    buffer.Put((byte)(value >> 7 | 0x80));
                    if (value >> 21 == 0)
                    {
                        buffer.Put((byte)(value >> 14));
                    }
                    else {
                        buffer.Put((byte)(value >> 14 | 0x80));
                        if (value >> 28 == 0)
                        {
                            buffer.Put((byte)(value >> 21));
                        }
                        else {
                            buffer.Put((byte)(value >> 21 | 0x80));
                            if (value >> 35 == 0)
                            {
                                buffer.Put((byte)(value >> 28));
                            }
                            else {
                                buffer.Put((byte)(value >> 28 | 0x80));
                                if (value >> 42 == 0)
                                {
                                    buffer.Put((byte)(value >> 35));
                                }
                                else {
                                    buffer.Put((byte)(value >> 35 | 0x80));
                                    if (value >> 49 == 0)
                                    {
                                        buffer.Put((byte)(value >> 42));
                                    }
                                    else {
                                        buffer.Put((byte)(value >> 42 | 0x80));
                                        if (value >> 56 == 0)
                                        {
                                            buffer.Put((byte)(value >> 49));
                                        }
                                        else {
                                            buffer.Put((byte)(value >> 49 | 0x80));
                                            buffer.Put((byte)(value >> 56));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads an LEB128-64b9B ZigZag encoded 64 bit integer (<see cref="long"/>) value from the given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <returns>The value read from the buffer.</returns>
        public static long GetLong(ByteBuffer buffer)
        {
            long v = buffer.Get();
            long value = v & 0x7F;
            if ((v & 0x80) != 0)
            {
                v = buffer.Get();
                value |= (v & 0x7F) << 7;
                if ((v & 0x80) != 0)
                {
                    v = buffer.Get();
                    value |= (v & 0x7F) << 14;
                    if ((v & 0x80) != 0)
                    {
                        v = buffer.Get();
                        value |= (v & 0x7F) << 21;
                        if ((v & 0x80) != 0)
                        {
                            v = buffer.Get();
                            value |= (v & 0x7F) << 28;
                            if ((v & 0x80) != 0)
                            {
                                v = buffer.Get();
                                value |= (v & 0x7F) << 35;
                                if ((v & 0x80) != 0)
                                {
                                    v = buffer.Get();
                                    value |= (v & 0x7F) << 42;
                                    if ((v & 0x80) != 0)
                                    {
                                        v = buffer.Get();
                                        value |= (v & 0x7F) << 49;
                                        if ((v & 0x80) != 0)
                                        {
                                            v = buffer.Get();
                                            value |= v << 56;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            var unsignRightShiftedValue = (long)((ulong)value >> 1);
            value = unsignRightShiftedValue ^ (-(value & 1));
            return value;
        }
    }
}
