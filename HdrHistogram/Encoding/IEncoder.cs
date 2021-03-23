using HdrHistogram.Utilities;

namespace HdrHistogram.Encoding
{
    /// <summary>
    /// Defines a method to allow histogram data to be encoded into a <see cref="ByteBuffer"/>.
    /// </summary>
    public interface IEncoder
    {
        /// <summary>
        /// Encodes the supplied <see cref="IRecordedData"/> into the supplied <see cref="ByteBuffer"/>.
        /// </summary>
        /// <param name="data">The data to encode.</param>
        /// <param name="buffer">The target <see cref="ByteBuffer"/> to write to.</param>
        /// <returns>The number of bytes written.</returns>
        int Encode(IRecordedData data, ByteBuffer buffer);
    }
}