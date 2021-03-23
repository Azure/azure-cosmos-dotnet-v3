using System.IO;
using System.IO.Compression;

namespace HdrHistogram.Utilities
{
    internal static class ByteBufferExtensions
    {
        /// <summary>
        /// Copies compressed contents from <paramref name="source"/> into the <paramref name="target"/> from the <paramref name="targetOffset"/>
        /// </summary>
        /// <param name="target">The <see cref="ByteBuffer"/> that will be written to.</param>
        /// <param name="source">The source <see cref="ByteBuffer"/> to read the data from.</param>
        /// <param name="targetOffset">The <paramref name="target"/> buffer's offset to start writing from.</param>
        /// <returns>The number of bytes written.</returns>
        public static int CompressedCopy(this ByteBuffer target, ByteBuffer source, int targetOffset)
        {
            byte[] compressed;
            using (var ms = new MemoryStream(source.ToArray()))
            {
                compressed = Compress(ms);
            }
            target.BlockCopy(compressed, 0, targetOffset, compressed.Length);
            return compressed.Length;
        }

        private static byte[] Compress(Stream input)
        {
            using (var compressStream = new MemoryStream())
            {
                //Add the RFC 1950 headers.
                compressStream.WriteByte(0x58);
                compressStream.WriteByte(0x85);
                using (var compressor = new DeflateStream(compressStream, CompressionMode.Compress, leaveOpen: true))
                {
                    input.CopyTo(compressor);
                }
                return compressStream.ToArray();
            }
        }
    }
}