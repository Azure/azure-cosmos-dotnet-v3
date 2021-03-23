/*
 * Written by Matt Warren, and released to the public domain,
 * as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 *
 * This is a .NET port of the original Java version, which was written by
 * Gil Tene as described in
 * https://github.com/HdrHistogram/HdrHistogram
 */

using System;
using System.Net;

namespace HdrHistogram.Utilities
{
    // See http://stackoverflow.com/questions/1261543/equivalent-of-javas-bytebuffer-puttype-in-c-sharp
    // and http://stackoverflow.com/questions/18040012/what-is-the-equivalent-of-javas-bytebuffer-wrap-in-c
    // and http://stackoverflow.com/questions/1261543/equivalent-of-javas-bytebuffer-puttype-in-c-sharp
    // Java version http://docs.oracle.com/javase/7/docs/api/java/nio/ByteBuffer.html
    /// <summary>
    /// A byte buffer that tracks position and allows reads and writes of 32 and 64 bit integer values.
    /// </summary>
    public sealed class ByteBuffer
    {
        private readonly byte[] _internalBuffer;

        /// <summary>
        /// Creates a <see cref="ByteBuffer"/> with a specified capacity in bytes.
        /// </summary>
        /// <param name="bufferCapacity">The capacity of the buffer in bytes</param>
        /// <returns>A newly created <see cref="ByteBuffer"/>.</returns>
        public static ByteBuffer Allocate(int bufferCapacity)
        {
            return new ByteBuffer(bufferCapacity);
        }

        /// <summary>
        /// Creates a <see cref="ByteBuffer"/> loaded with the provided byte array.
        /// </summary>
        /// <param name="source">The source byte array to load the buffer with.</param>
        /// <returns>A newly created <see cref="ByteBuffer"/>.</returns>
        public static ByteBuffer Allocate(byte[] source)
        {
            var buffer = new ByteBuffer(source.Length);
            Buffer.BlockCopy(source, 0, buffer._internalBuffer, buffer.Position, source.Length);
            return buffer;
        }

        private ByteBuffer(int bufferCapacity)
        {
            _internalBuffer = new byte[bufferCapacity];
            Position = 0;
        }

        /// <summary>
        /// The buffer's current position in the underlying byte array
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// Returns the capacity of the <see cref="ByteBuffer"/>
        /// </summary>
        /// <returns>The length of the internal byte array.</returns>
        public int Capacity()
        {
            return _internalBuffer.Length;
        }

        /// <summary>
        /// The remaining capacity.
        /// </summary>
        /// <returns>The number of bytes between the current position and the underlying byte array length.</returns>
        public int Remaining()
        {
            return Capacity() - Position;
        }

        /// <summary>
        /// Reads from the provided <see cref="System.IO.Stream"/>, into the buffer.
        /// </summary>
        /// <param name="source">The source stream to read from.</param>
        /// <param name="length">The number of bytes to read.</param>
        /// <returns>The number of bytes read.</returns>
        public int ReadFrom(System.IO.Stream source, int length)
        {
            return source.Read(_internalBuffer, Position, length);
        }

        /// <summary>
        /// Gets the current byte and advances the position by one.
        /// </summary>
        /// <returns>The byte at the current position.</returns>
        public byte Get()
        {
            return _internalBuffer[Position++];
        }

        /// <summary>
        /// Gets the 16 bit integer (<seealso cref="short"/>) at the current position, and then advances by two.
        /// </summary>
        /// <returns>The value of the <see cref="short"/> at the current position.</returns>
        public short GetShort()
        {
            var shortValue = IPAddress.HostToNetworkOrder(BitConverter.ToInt16(_internalBuffer, Position));
            Position += (sizeof(short));
            return shortValue;
        }

        /// <summary>
        /// Gets the 32 bit integer (<seealso cref="int"/>) at the current position, and then advances by four.
        /// </summary>
        /// <returns>The value of the <see cref="int"/> at the current position.</returns>
        public int GetInt()
        {
            var intValue = IPAddress.HostToNetworkOrder(BitConverter.ToInt32(_internalBuffer, Position));
            Position += sizeof(int);
            return intValue;
        }

        /// <summary>
        /// Gets the 64 bit integer (<seealso cref="long"/>) at the current position, and then advances by eight.
        /// </summary>
        /// <returns>The value of the <see cref="long"/> at the current position.</returns>
        public long GetLong()
        {
            var longValue = IPAddress.HostToNetworkOrder(BitConverter.ToInt64(_internalBuffer, Position));
            Position += sizeof(long);
            return longValue;
        }

        /// <summary>
        /// Gets the double floating point number (<seealso cref="double"/>) at the current position, and then advances by eight.
        /// </summary>
        /// <returns>The value of the <see cref="double"/> at the current position.</returns>
        public double GetDouble()
        {
            var doubleValue = Int64BitsToDouble(ToInt64(_internalBuffer, Position));
            Position += sizeof(double);
            return doubleValue;
        }

        /// <summary>
        /// Converts the specified 64-bit signed integer to a double-precision 
        /// floating point number. Note: the endianness of this converter does not
        /// affect the returned value.
        /// </summary>
        /// <param name="value">The number to convert. </param>
        /// <returns>A double-precision floating point number whose value is equivalent to value.</returns>
        private static double Int64BitsToDouble(long value)
        {
            return BitConverter.Int64BitsToDouble(value);
        }

        /// <summary>
        /// Returns a 64-bit signed integer converted from eight bytes at a specified position in a byte array.
        /// </summary>
        /// <param name="value">An array of bytes.</param>
        /// <param name="startIndex">The starting position within value.</param>
        /// <returns>A 64-bit signed integer formed by eight bytes beginning at startIndex.</returns>
        private static long ToInt64(byte[] value, int startIndex)
        {
            return CheckedFromBytes(value, startIndex, 8);
        }

        /// <summary>
        /// Checks the arguments for validity before calling FromBytes
        /// (which can therefore assume the arguments are valid).
        /// </summary>
        /// <param name="value">The bytes to convert after checking</param>
        /// <param name="startIndex">The index of the first byte to convert</param>
        /// <param name="bytesToConvert">The number of bytes to convert</param>
        /// <returns></returns>
        private static long CheckedFromBytes(byte[] value, int startIndex, int bytesToConvert)
        {
            CheckByteArgument(value, startIndex, bytesToConvert);
            return FromBytes(value, startIndex, bytesToConvert);
        }

        /// <summary>
        /// Checks the given argument for validity.
        /// </summary>
        /// <param name="value">The byte array passed in</param>
        /// <param name="startIndex">The start index passed in</param>
        /// <param name="bytesRequired">The number of bytes required</param>
        /// <exception cref="ArgumentNullException">value is a null reference</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// startIndex is less than zero or greater than the length of value minus bytesRequired.
        /// </exception>
        private static void CheckByteArgument(byte[] value, int startIndex, int bytesRequired)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            if (startIndex < 0 || startIndex > value.Length - bytesRequired)
            {
                throw new ArgumentOutOfRangeException(nameof(startIndex));
            }
        }

        /// <summary>
        /// Returns a value built from the specified number of bytes from the given buffer,
        /// starting at index.
        /// </summary>
        /// <param name="buffer">The data in byte array format</param>
        /// <param name="startIndex">The first index to use</param>
        /// <param name="bytesToConvert">The number of bytes to use</param>
        /// <returns>The value built from the given bytes</returns>
        private static long FromBytes(byte[] buffer, int startIndex, int bytesToConvert)
        {
            long ret = 0;
            for (int i = 0; i < bytesToConvert; i++)
            {
                ret = unchecked((ret << 8) | buffer[startIndex + i]);
            }
            return ret;
        }

        /// <summary>
        /// Writes a byte value to the current position, and advances the position by one.
        /// </summary>
        /// <param name="value">The byte value to write.</param>
        public void Put(byte value)
        {
            _internalBuffer[Position++] = value;
        }

        /// <summary>
        /// Sets the bytes at the current position to the value of the passed value, and advances the position.
        /// </summary>
        /// <param name="value">The value to set the current position to.</param>
        public void PutInt(int value)
        {
            var intAsBytes = BitConverter.GetBytes(IPAddress.NetworkToHostOrder(value));
            Array.Copy(intAsBytes, 0, _internalBuffer, Position, intAsBytes.Length);
            Position += intAsBytes.Length;
        }

        /// <summary>
        /// Sets the bytes at the provided position to the value of the passed value, and does not advance the position.
        /// </summary>
        /// <param name="index">The position to set the value at.</param>
        /// <param name="value">The value to set.</param>
        /// <remarks>
        /// This can be useful for writing a value into an earlier placeholder e.g. a header property for storing the body length.
        /// </remarks>
        public void PutInt(int index, int value)
        {
            var intAsBytes = BitConverter.GetBytes(IPAddress.NetworkToHostOrder(value));
            Array.Copy(intAsBytes, 0, _internalBuffer, index, intAsBytes.Length);
            // We don't increment the Position as this is an explicit write.
        }

        /// <summary>
        /// Sets the bytes at the current position to the value of the passed value, and advances the position.
        /// </summary>
        /// <param name="value">The value to set the current position to.</param>
        public void PutLong(long value)
        {
            var longAsBytes = BitConverter.GetBytes(IPAddress.NetworkToHostOrder(value));
            Array.Copy(longAsBytes, 0, _internalBuffer, Position, longAsBytes.Length);
            Position += longAsBytes.Length;
        }

        /// <summary>
        /// Sets the bytes at the current position to the value of the passed value, and advances the position.
        /// </summary>
        /// <param name="value">The value to set the current position to.</param>
        public void PutDouble(double value)
        {
            //PutDouble(ix(CheckIndex(i, (1 << 3))), x);
            var doubleAsBytes = BitConverter.GetBytes(value);
            Array.Reverse(doubleAsBytes);
            Array.Copy(doubleAsBytes, 0, _internalBuffer, Position, doubleAsBytes.Length);
            Position += doubleAsBytes.Length;
        }

        /// <summary>
        /// Gets a copy of the internal byte array.
        /// </summary>
        /// <returns>The a copy of the internal byte array.</returns>
        internal byte[] ToArray()
        {
            var copy = new byte[_internalBuffer.Length];
            Array.Copy(_internalBuffer, copy, _internalBuffer.Length);
            return copy;
        }

        internal void BlockCopy(Array src, int srcOffset, int dstOffset, int count)
        {
            Buffer.BlockCopy(src: src, srcOffset: srcOffset, dst: _internalBuffer, dstOffset: dstOffset, count: count);
            Position += count;
        }

        internal void BlockGet(Array target, int targetOffset, int sourceOffset, int count)
        {
            Buffer.BlockCopy(src: _internalBuffer, srcOffset: sourceOffset, dst: target, dstOffset: targetOffset, count: count);
        }
    }
}