//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.IO;
    using System.Text;

    /// <summary>
    /// A BinaryReader that can read binary that has little endian byte ordering for a client that can be run a both a big endian and little endian machine.
    /// </summary>
    internal sealed class LittleEndianBinaryReader : BinaryReader
    {
        /// <summary>
        /// Flag used to determine if the machine is a little endian machine.
        /// </summary>
        private readonly bool isLittleEndian;

        /// <summary>
        /// Initializes a new instance of the <see cref="LittleEndianBinaryReader"/> class based on the specified stream and using UTF-8 encoding.
        /// </summary>
        /// <param name="input">The input stream.</param>
        public LittleEndianBinaryReader(Stream input)
            : this(input, Encoding.UTF8)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LittleEndianBinaryReader"/> class based on the specified stream and character encoding.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="encoding">The character encoding to use.</param>
        public LittleEndianBinaryReader(Stream input, Encoding encoding)
            : this(input, encoding, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LittleEndianBinaryReader"/> class based on the specified stream and character encoding, and optionally leaves the stream open.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <param name="leaveOpen">true to leave the stream open after the System.IO.BinaryReader object is disposed; otherwise, false.</param>
        public LittleEndianBinaryReader(Stream input, Encoding encoding, bool leaveOpen)
            : this(input, encoding, leaveOpen, BitConverter.IsLittleEndian)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LittleEndianBinaryReader"/> class for testing purposes, since we don't want the user to be able to tell us what the endianness of the machine is.
        /// Thus this constructor is private.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <param name="leaveOpen">true to leave the stream open after the System.IO.BinaryReader object is disposed; otherwise, false.</param>
        /// <param name="isLittleEndian">Whether the machine is a little endian machine</param>
        private LittleEndianBinaryReader(Stream input, Encoding encoding, bool leaveOpen, bool isLittleEndian)
            : base(input, encoding, leaveOpen)
        {
            this.isLittleEndian = isLittleEndian;
        }

        /// <summary>
        /// Reads a Boolean value from the current stream and advances the current position of the stream by one byte.
        /// </summary>
        /// <returns>true if the byte is nonzero; otherwise, false.</returns>
        public override bool ReadBoolean()
        {
            // Bools are just 1 byte so we can just return what the base class does.
            return base.ReadBoolean();
        }

        /// <summary>
        /// Reads the next byte from the current stream and advances the current position of the stream by one byte.
        /// </summary>
        /// <returns>The next byte read from the current stream.</returns>
        public override byte ReadByte()
        {
            // A single byte has no byte order so we can just pass it down to the base class
            return base.ReadByte();
        }

        /// <summary>
        /// Reads the next decimal from the current stream and advances the current position of the stream by 16 bytes.
        /// </summary>
        /// <returns>The next decimal read from the current stream.</returns>
        public override decimal ReadDecimal()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Reads an 8-byte floating point value from the current stream and advances the current position of the stream by eight bytes.
        /// </summary>
        /// <returns>An 8-byte floating point value read from the current stream.</returns>
        public override double ReadDouble()
        {
            return this.isLittleEndian ? base.ReadDouble() : ByteOrder.Reverse(base.ReadDouble());
        }

        /// <summary>
        /// Reads a 2-byte signed integer from the current stream and advances the current position of the stream by two bytes.
        /// </summary>
        /// <returns> A 2-byte signed integer read from the current stream.</returns>
        public override short ReadInt16()
        {
            return this.isLittleEndian ? base.ReadInt16() : ByteOrder.Reverse(base.ReadInt16());
        }

        /// <summary>
        /// Reads a 4-byte signed integer from the current stream and advances the current position of the stream by four bytes.
        /// </summary>
        /// <returns>A 4-byte signed integer read from the current stream.</returns>
        public override int ReadInt32()
        {
            return this.isLittleEndian ? base.ReadInt32() : ByteOrder.Reverse(base.ReadInt32());
        }

        /// <summary>
        /// Reads an 8-byte signed integer from the current stream and advances the current position of the stream by eight bytes.
        /// </summary>
        /// <returns>An 8-byte signed integer read from the current stream.</returns>
        public override long ReadInt64()
        {
            return this.isLittleEndian ? base.ReadInt64() : ByteOrder.Reverse(base.ReadInt64());
        }

        /// <summary>
        /// Reads a signed byte from this stream and advances the current position of the stream by one byte.
        /// </summary>
        /// <returns>A signed byte read from the current stream.</returns>
        public override sbyte ReadSByte()
        {
            // A single byte has no byte order.
            return base.ReadSByte();
        }

        /// <summary>
        /// Reads a 4-byte floating point value from the current stream and advances the current position of the stream by four bytes.
        /// </summary>
        /// <returns>A 4-byte floating point value read from the current stream.</returns>
        public override float ReadSingle()
        {
            return this.isLittleEndian ? base.ReadSingle() : ByteOrder.Reverse(base.ReadSingle());
        }

        /// <summary>
        /// Reads a string from the current stream. The string is prefixed with the length, encoded as an integer seven bits at a time.
        /// </summary>
        /// <returns>The string being read.</returns>
        public override string ReadString()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Reads a 2-byte unsigned integer from the current stream and advances the position of the stream by two bytes.
        /// </summary>
        /// <returns>A 2-byte unsigned integer from the current stream</returns>
        public override ushort ReadUInt16()
        {
            return this.isLittleEndian ? base.ReadUInt16() : ByteOrder.Reverse(base.ReadUInt16());
        }

        /// <summary>
        /// Reads a 4-byte unsigned integer from the current stream and advances the position of the stream by four bytes.
        /// </summary>
        /// <returns>A 4-byte unsigned integer read from this stream.</returns>
        public override uint ReadUInt32()
        {
            return this.isLittleEndian ? base.ReadUInt32() : ByteOrder.Reverse(base.ReadUInt32());
        }

        /// <summary>
        /// Reads an 8-byte unsigned integer from the current stream and advances the position of the stream by eight bytes.
        /// </summary>
        /// <returns>An 8-byte unsigned integer read from this stream.</returns>
        public override ulong ReadUInt64()
        {
            return this.isLittleEndian ? base.ReadUInt64() : ByteOrder.Reverse(base.ReadUInt64());
        }
    }
}
