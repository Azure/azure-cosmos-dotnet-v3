//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// Static class with utility functions and constants for JSON binary encoding.
    /// </summary>
    internal static partial class JsonBinaryEncoding
    {
        /// <summary>
        /// A type marker is a single byte.
        /// </summary>
        public const int TypeMarkerLength = 1;

        /// <summary>
        /// Some type markers are followed by a single byte representing the length.
        /// </summary>
        public const int OneByteLength = 1;

        /// <summary>
        /// Some type markers are followed by 1 byte for the length and then optionally 1 byte for the count.
        /// </summary>
        public const int OneByteCount = 1;

        /// <summary>
        /// Some type markers are followed by 2 bytes representing the length as a ushort.
        /// </summary>
        public const int TwoByteLength = 2;

        /// <summary>
        /// Some type markers are followed by 2 bytes for the length and then optionally 2 bytes for the count (both are ushorts).
        /// </summary>
        public const int TwoByteCount = 2;

        /// <summary>
        /// Some type markers are followed by 4 bytes for representing the length as a uint32.
        /// </summary>
        public const int FourByteLength = 4;

        /// <summary>
        /// Some type markers are followed by 4 bytes for the length and then optionally 4 bytes for the count (both are uint32).
        /// </summary>
        public const int FourByteCount = 4;

        /// <summary>
        /// For compressed strings we use a single byte base character.
        /// </summary>
        public const int OneByteBaseChar = 1;

        /// <summary>
        /// Reference strings are followed by an offset; this is for 1 byte offset reference strings.
        /// </summary>
        public const int OneByteOffset = 1;

        /// <summary>
        /// Reference strings are followed by an offset; this is for 2 byte offset reference strings.
        /// </summary>
        public const int TwoByteOffset = 2;

        /// <summary>
        /// Reference strings are followed by an offset; this is for 2 byte offset reference strings.
        /// </summary>
        public const int ThreeByteOffset = 3;

        /// <summary>
        /// Reference strings are followed by an offset; this is for 4 byte offset reference strings.
        /// </summary>
        public const int FourByteOffset = 4;

        public static Guid GetGuidValue(ReadOnlySpan<byte> guidToken)
        {
            if (!JsonBinaryEncoding.TryGetGuidValue(guidToken, out Guid guidValue))
            {
                throw new JsonInvalidNumberException();
            }

            return guidValue;
        }

        public static bool TryGetGuidValue(
            ReadOnlySpan<byte> guidToken,
            out Guid guidValue)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<Guid>(
                guidToken,
                JsonBinaryEncoding.TypeMarker.Guid,
                out guidValue);
        }

        public static ReadOnlyMemory<byte> GetBinaryValue(ReadOnlyMemory<byte> binaryToken)
        {
            if (!JsonBinaryEncoding.TryGetBinaryValue(binaryToken, out ReadOnlyMemory<byte> binaryValue))
            {
                throw new JsonInvalidTokenException();
            }

            return binaryValue;
        }

        public static bool TryGetBinaryValue(
            ReadOnlyMemory<byte> binaryToken,
            out ReadOnlyMemory<byte> binaryValue)
        {
            binaryValue = default;
            if (binaryToken.Length < 1)
            {
                return false;
            }

            byte typeMarker = binaryToken.Span[0];
            // trim off the type marker
            binaryToken = binaryToken.Slice(1);

            uint length;
            switch (typeMarker)
            {
                case JsonBinaryEncoding.TypeMarker.Binary1ByteLength:
                    if (binaryToken.Length < JsonBinaryEncoding.OneByteLength)
                    {
                        return false;
                    }

                    length = MemoryMarshal.Read<byte>(binaryToken.Span);
                    binaryToken = binaryToken.Slice(JsonBinaryEncoding.OneByteLength);
                    break;

                case JsonBinaryEncoding.TypeMarker.Binary2ByteLength:
                    if (binaryToken.Length < JsonBinaryEncoding.TwoByteLength)
                    {
                        return false;
                    }

                    length = MemoryMarshal.Read<ushort>(binaryToken.Span);
                    binaryToken = binaryToken.Slice(JsonBinaryEncoding.TwoByteLength);
                    break;

                case JsonBinaryEncoding.TypeMarker.Binary4ByteLength:
                    if (binaryToken.Length < JsonBinaryEncoding.FourByteLength)
                    {
                        return false;
                    }

                    length = MemoryMarshal.Read<uint>(binaryToken.Span);
                    binaryToken = binaryToken.Slice(JsonBinaryEncoding.FourByteLength);
                    break;

                default:
                    return false;
            }

            if (length > int.MaxValue)
            {
                return false;
            }

            if (binaryToken.Length < length)
            {
                return false;
            }

            binaryValue = binaryToken.Slice(0, (int)length);
            return true;
        }

        /// <summary>
        /// Gets the length of a particular value given its TypeMarker.
        /// </summary>
        /// <param name="buffer">The buffer to read from as input.</param>
        /// <returns>
        /// - Positive Value: The length of the value including its TypeMarker
        /// - Negative Value: The length is encoded as an integer of size equals to abs(value) following The TypeMarker byte
        /// - Zero Value: The length is unknown (for instance an unassigned type marker)
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetValueLength(ReadOnlySpan<byte> buffer)
        {
            long valueLength = JsonBinaryEncoding.ValueLengths.GetValueLength(buffer);
            if (valueLength > int.MaxValue)
            {
                throw new InvalidOperationException($"{nameof(valueLength)} is greater than int.MaxValue");
            }

            return (int)valueLength;
        }

        /// <summary>
        /// Gets the length of a particular string given its TypeMarker.
        /// </summary>
        /// <param name="typeMarker">The type marker as input</param>
        /// <returns>
        /// - Non-Negative Value: The TypeMarker encodes the string length
        /// - Negative Value: System or user dictionary encoded string, or encoded string length that follows The TypeMarker
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetStringLengths(byte typeMarker)
        {
            return JsonBinaryEncoding.StringLengths.Lengths[typeMarker];
        }

        /// <summary>
        /// Gets the offset of the first item in an array or object
        /// </summary>
        /// <param name="typeMarker">The TypeMarker as input.</param>
        /// <returns>The offset of the first item in an array or object</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetFirstValueOffset(byte typeMarker)
        {
            return JsonBinaryEncoding.FirstValueOffsets.Offsets[typeMarker];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryGetValueLength(ReadOnlySpan<byte> buffer, out int length)
        {
            // Too lazy to convert this right now.
            length = JsonBinaryEncoding.GetValueLength(buffer);
            return true;
        }

        private static bool TryGetFixedWidthValue<T>(
            ReadOnlySpan<byte> token,
            int expectedTypeMarker,
            out T fixedWidthValue)
            where T : struct
        {
            fixedWidthValue = default;
            int sizeofType = Marshal.SizeOf(fixedWidthValue);
            if (token.Length < 1 + sizeofType)
            {
                return false;
            }

            byte typeMarker = token[0];
            if (typeMarker != expectedTypeMarker)
            {
                return false;
            }

            fixedWidthValue = MemoryMarshal.Read<T>(token.Slice(1));
            return true;
        }

        public static void SetFixedSizedValue<TFixedType>(Span<byte> buffer, TFixedType value)
            where TFixedType : struct
        {
            Span<TFixedType> bufferAsFixedType = MemoryMarshal.Cast<byte, TFixedType>(buffer);
            bufferAsFixedType[0] = value;
        }

        public static TFixedType GetFixedSizedValue<TFixedType>(ReadOnlySpan<byte> buffer)
            where TFixedType : struct
        {
            return MemoryMarshal.Read<TFixedType>(buffer);
        }

        public static string HexDump(byte[] bytes, int bytesPerLine = 16)
        {
            //https://www.codeproject.com/Articles/36747/Quick-and-Dirty-HexDump-of-a-Byte-Array
            StringBuilder sb = new StringBuilder();
            for (int line = 0; line < bytes.Length; line += bytesPerLine)
            {
                byte[] lineBytes = bytes.Skip(line).Take(bytesPerLine).ToArray();
                sb.AppendFormat("{0:x8} ", line);
                sb.Append(string.Join(" ", lineBytes.Select(b => b.ToString("x2"))
                       .ToArray()).PadRight(bytesPerLine * 3));
                sb.Append(" ");
                sb.Append(new string(lineBytes.Select(b => b < 32 ? '.' : (char)b)
                       .ToArray()));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        [StructLayout(LayoutKind.Sequential, Size = 3)]
        public readonly struct UInt24
        {
            public static readonly UInt24 MinValue = new UInt24(byte1: byte.MinValue, byte2: byte.MinValue, byte3: byte.MinValue);
            public static readonly UInt24 MaxValue = new UInt24(byte1: byte.MaxValue, byte2: byte.MaxValue, byte3: byte.MaxValue);

            public UInt24(byte byte1, byte byte2, byte byte3)
            {
                this.Byte1 = byte1;
                this.Byte2 = byte2;
                this.Byte3 = byte3;
            }

            public byte Byte1 { get; }
            public byte Byte2 { get; }
            public byte Byte3 { get; }

            public static implicit operator int(UInt24 value)
            {
                return (value.Byte3 << 16) | (value.Byte2 << 8) | (value.Byte1 << 0);
            }

            public static explicit operator UInt24(int value)
            {
                if ((value & 0xFF000000) != 0)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(value)} must not have any of the top 8 bits set.");
                }

                return new UInt24(
                    byte1: (byte)((value & 0x000000FF) >> 0),
                    byte2: (byte)((value & 0x0000FF00) >> 8),
                    byte3: (byte)((value & 0x00FF0000) >> 16));
            }
        }
    }
}
