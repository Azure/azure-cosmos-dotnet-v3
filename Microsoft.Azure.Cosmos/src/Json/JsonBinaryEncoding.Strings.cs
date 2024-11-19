// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Cosmos.Core;
    using Microsoft.Azure.Cosmos.Core.Utf8;

    internal static partial class JsonBinaryEncoding
    {
        public const int GuidLength = 36;
        public const int GuidWithQuotesLength = GuidLength + 2;
        public const int EncodedGuidLength = 17;

        private const int MaxStackAlloc = 4 * 1024;
        private const int Min4BitCharSetStringLength = 16;
        private const int MinCompressedStringLength4 = 24;
        private const int MinCompressedStringLength5 = 32;
        private const int MinCompressedStringLength6 = 40;
        private const int MinCompressedStringLength7 = 88;
        private const int MinCompressedStringLength = Min4BitCharSetStringLength;

        /// <summary>
        ///  Determines whether a type-marker is potentially for a buffered string value
        /// </summary>
        private static readonly ImmutableArray<bool> IsBufferedStringCandidate = new bool[]
        {
            // Encoded literal integer value (32 values)
            false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false,
            false, false, false, false, false, false, false, false,

            // Encoded 0-byte system string (32 values)
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,

            // Encoded true-byte user string (32 values)
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,

            // Encoded 2-byte user string (16 values)
            true, true, true, true, true, true, true, true,

            // String Values [0x68, 0x70)
            false,  // <empty> 0x68
            false,  // <empty> 0x69
            false,  // <empty> 0x6A
            false,  // <empty> 0x6B
            false,  // <empty> 0x6C
            false,  // <empty> 0x6D
            false,  // <empty> 0x6E
            false,  // <empty> 0x6F

            // String Values [0x70, 0x78)
            false,  // <empty> 0x70
            false,  // <empty> 0x71
            false,  // <empty> 0x72
            false,  // <empty> 0x73
            false,  // <empty> 0x74
            false,  // StrGL (Lowercase GUID string)
            false,  // StrGU (Uppercase GUID string)
            false,  // StrGQ (Double-quoted lowercase GUID string)

            // Compressed strings [falsex78, falsex8false)
            false,  // String 1-byte length - Lowercase hexadecimal digits encoded as 4-bit characters
            false,  // String 1-byte length - Uppercase hexadecimal digits encoded as 4-bit characters
            false,  // String 1-byte length - Date-time character set encoded as 4-bit characters
            false,  // String 1-byte Length - 4-bit packed characters relative to a base value
            false,  // String 1-byte Length - 5-bit packed characters relative to a base value
            false,  // String 1-byte Length - 6-bit packed characters relative to a base value
            false,  // String 1-byte Length - 7-bit packed characters
            false,  // String 2-byte Length - 7-bit packed characters

            // TypeMarker-encoded string length (64 values)
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,
            true, true, true, true, true, true, true, true,

            // Variable Length String Values
            true,   // StrL1 (1-byte length)
            true,   // StrL2 (2-byte length)
            true,   // StrL4 (4-byte length)
            true,   // StrR1 (Reference string of 1-byte offset)
            true,   // StrR2 (Reference string of 2-byte offset)
            true,   // StrR3 (Reference string of 3-byte offset)
            true,   // StrR4 (Reference string of 4-byte offset)
            false,  // <empty> 0xC7

            // Numeric Values
            false,  // NumUI8
            false,  // NumI16,
            false,  // NumI32,
            false,  // NumI64,
            false,  // NumDbl,
            false,  // Float32
            false,  // Float64
            false,  // <empty> 0xCF

            // Other Value Types
            false,  // Null
            false,  // False
            false,  // True
            false,  // GUID
            false,  // <empty> 0xD4
            false,  // <empty> 0xD5
            false,  // <empty> 0xD6
            false,  // <empty> 0xD7

            false,  // Int8
            false,  // Int16
            false,  // Int32
            false,  // Int64
            false,  // UInt32
            false,  // BinL1 (1-byte length)
            false,  // BinL2 (2-byte length)
            false,  // BinL4 (4-byte length)

            // Array Type Markers
            false,  // Arr0
            false,  // Arr1
            false,  // ArrL1 (1-byte length)
            false,  // ArrL2 (2-byte length)
            false,  // ArrL4 (4-byte length)
            false,  // ArrLC1 (1-byte length and count)
            false,  // ArrLC2 (2-byte length and count)
            false,  // ArrLC4 (4-byte length and count)

            // Object Type Markers
            false,  // Obj0
            false,  // Obj1
            false,  // ObjL1 (1-byte length)
            false,  // ObjL2 (2-byte length)
            false,  // ObjL4 (4-byte length)
            false,  // ObjLC1 (1-byte length and count)
            false,  // ObjLC2 (2-byte length and count)
            false,  // ObjLC4 (4-byte length and count)

            // Empty Range
            false,  // <empty> 0xF0
            false,  // <empty> 0xF1
            false,  // <empty> 0xF2
            false,  // <empty> 0xF3
            false,  // <empty> 0xF4
            false,  // <empty> 0xF5
            false,  // <empty> 0xF7
            false,  // <empty> 0xF8

            // Special Values
            false,  // <special value reserved> 0xF8
            false,  // <special value reserved> 0xF9
            false,  // <special value reserved> 0xFA
            false,  // <special value reserved> 0xFB
            false,  // <special value reserved> 0xFC
            false,  // <special value reserved> 0xFD
            false,  // <special value reserved> 0xFE
            false,  // Invalid 0xFF
        }.ToImmutableArray();

        public static string GetStringValue(
            ReadOnlyMemory<byte> buffer,
            ReadOnlyMemory<byte> stringToken)
        {
            // First retrieve the string length
            GetStringValue(buffer, stringToken, destinationBuffer: Span<byte>.Empty, out int valueLength);

            Span<byte> destinationBuffer = valueLength < MaxStackAlloc ? stackalloc byte[valueLength] : new byte[valueLength];
            GetStringValue(buffer, stringToken, destinationBuffer, out valueLength);

            return Utf8Span.UnsafeFromUtf8BytesNoValidation(destinationBuffer).ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Utf8Span GetUtf8SpanValue(
            ReadOnlyMemory<byte> buffer,
            ReadOnlyMemory<byte> stringToken)
        {
            return Utf8Span.UnsafeFromUtf8BytesNoValidation(GetUtf8MemoryValue(buffer, stringToken).Span);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Utf8String GetUtf8StringValue(
            ReadOnlyMemory<byte> buffer,
            ReadOnlyMemory<byte> stringToken)
        {
            return Utf8String.UnsafeFromUtf8BytesNoValidation(GetUtf8MemoryValue(buffer, stringToken));
        }

        public static bool TryGetBufferedStringValue(
            ReadOnlyMemory<byte> buffer,
            ReadOnlyMemory<byte> stringToken,
            out Utf8Memory value)
        {
            if (stringToken.IsEmpty)
            {
                value = default;
                return false;
            }

            if (JsonBinaryEncoding.TryGetBufferedLengthPrefixedString(
                buffer,
                stringToken,
                out value))
            {
                return true;
            }

            if (JsonBinaryEncoding.TryGetEncodedStringValue(
                stringToken.Span,
                out UtfAllString encodedStringValue))
            {
                value = encodedStringValue.Utf8EscapedString;
                return true;
            }

            value = default;
            return false;
        }

        public static bool TryGetDictionaryEncodedStringValue(
            ReadOnlySpan<byte> stringToken,
            out UtfAllString value) => TryGetEncodedStringValue(
                stringToken,
                out value);

        private static ReadOnlyMemory<byte> GetUtf8MemoryValue(
            ReadOnlyMemory<byte> buffer,
            ReadOnlyMemory<byte> stringToken)
        {
            byte typeMarker = stringToken.Span[0];

            if (IsBufferedStringCandidate[typeMarker])
            {
                if (!TryGetBufferedStringValue(
                    buffer,
                    stringToken,
                    out Utf8Memory bufferedStringValue))
                {
                    throw new JsonInvalidTokenException();
                }

                return bufferedStringValue.Memory;
            }

            if (JsonBinaryEncoding.TypeMarker.IsCompressedString(typeMarker) || JsonBinaryEncoding.TypeMarker.IsGuidString(typeMarker))
            {
                DecodeString(stringToken.Span, Span<byte>.Empty, out int valueLength);
                Memory<byte> bytes = new byte[valueLength];
                DecodeString(stringToken.Span, bytes.Span, out valueLength);
                return bytes;
            }

            throw new JsonInvalidTokenException();
        }

        private static void GetStringValue(
            ReadOnlyMemory<byte> buffer,
            ReadOnlyMemory<byte> stringToken,
            Span<byte> destinationBuffer,
            out int valueLength)
        {
            if (stringToken.IsEmpty)
            {
                throw new JsonInvalidTokenException();
            }

            byte typeMarker = stringToken.Span[0];
            if (IsBufferedStringCandidate[typeMarker])
            {
                if (!TryGetBufferedStringValue(
                    buffer,
                    stringToken,
                    out Utf8Memory bufferedStringValue))
                {
                    throw new JsonInvalidTokenException();
                }

                if (!destinationBuffer.IsEmpty)
                {
                    if (bufferedStringValue.Length > destinationBuffer.Length)
                    {
                        throw new InvalidOperationException("buffer is too small.");
                    }

                    bufferedStringValue.Memory.Span.CopyTo(destinationBuffer);
                }

                valueLength = bufferedStringValue.Length;
            }
            else if (JsonBinaryEncoding.TypeMarker.IsCompressedString(typeMarker) || JsonBinaryEncoding.TypeMarker.IsGuidString(typeMarker))
            {
                DecodeString(stringToken.Span, destinationBuffer, out valueLength);
            }
            else
            {
                throw new JsonInvalidTokenException();
            }
        }

        /// <summary>
        /// Try Get Encoded String Value
        /// </summary>
        /// <param name="stringToken">The string token to read from.</param>
        /// <param name="value">The encoded string if found.</param>
        /// <returns>Encoded String Value</returns>
        private static bool TryGetEncodedStringValue(
            ReadOnlySpan<byte> stringToken,
            out UtfAllString value)
        {
            if (JsonBinaryEncoding.TryGetEncodedSystemStringValue(stringToken, out value))
            {
                return true;
            }

            if (JsonBinaryEncoding.TryGetEncodedUserStringValue(stringToken, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Try Get Encoded System String Value
        /// </summary>
        /// <param name="stringToken">The buffer to read from..</param>
        /// <param name="value">The encoded system string.</param>
        /// <returns>Encoded System String Value</returns>
        private static bool TryGetEncodedSystemStringValue(
            ReadOnlySpan<byte> stringToken,
            out UtfAllString value)
        {
            if (!JsonBinaryEncoding.TypeMarker.IsSystemString(stringToken[0]))
            {
                value = default;
                return false;
            }

            if (stringToken.Length < 1)
            {
                value = default;
                return false;
            }

            int systemStringId = stringToken[0] - JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin;
            return JsonBinaryEncoding.SystemStrings.TryGetSystemStringById(systemStringId, out value);
        }

        /// <summary>
        /// Try Get Encoded User String Value
        /// </summary>
        /// <param name="stringToken">The string token to read from.</param>
        /// <param name="encodedUserStringValue">The encoded user string value if found.</param>
        /// <returns>Whether or not the Encoded User String Value was found</returns>
        private static bool TryGetEncodedUserStringValue(
            ReadOnlySpan<byte> stringToken,
            out UtfAllString encodedUserStringValue)
        {
            encodedUserStringValue = default;
            return false;
        }

        private static bool TryGetUserStringId(ReadOnlySpan<byte> stringToken, out int userStringId)
        {
            byte typeMarker = stringToken[0];
            if (!JsonBinaryEncoding.TypeMarker.IsUserString(typeMarker))
            {
                userStringId = default;
                return false;
            }

            if (JsonBinaryEncoding.TypeMarker.IsOneByteEncodedUserString(typeMarker))
            {
                if (stringToken.Length < 1)
                {
                    userStringId = default;
                    return false;
                }

                userStringId = typeMarker - JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin;
            }
            else //// JsonBinaryEncoding.TypeMarker.IsTwoByteEncodedUserString(typeMarker)
            {
                if (stringToken.Length < 2)
                {
                    userStringId = default;
                    return false;
                }

                const byte OneByteCount = JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMax - JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin;
                userStringId = OneByteCount
                    + stringToken[1]
                    + ((stringToken[0] - JsonBinaryEncoding.TypeMarker.UserString2ByteLengthMin) * 0xFF);
            }

            return true;
        }

        private static bool TryGetBufferedLengthPrefixedString(
            ReadOnlyMemory<byte> buffer,
            ReadOnlyMemory<byte> stringToken,
            out Utf8Memory value)
        {
            ReadOnlySpan<byte> stringTokenSpan = stringToken.Span;
            byte typeMarker = stringTokenSpan[0];
            stringTokenSpan = stringTokenSpan.Slice(start: 1);

            int start;
            long length;
            if (JsonBinaryEncoding.TypeMarker.IsEncodedLengthString(typeMarker))
            {
                start = JsonBinaryEncoding.TypeMarkerLength;
                length = JsonBinaryEncoding.GetStringLengths(typeMarker);
            }
            else
            {
                switch (typeMarker)
                {
                    case JsonBinaryEncoding.TypeMarker.StrL1:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.OneByteLength)
                        {
                            value = default;
                            return false;
                        }

                        start = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength;
                        length = stringTokenSpan[0];
                        break;

                    case JsonBinaryEncoding.TypeMarker.StrL2:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.TwoByteLength)
                        {
                            value = default;
                            return false;
                        }

                        start = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.TwoByteLength;
                        length = MemoryMarshal.Read<ushort>(stringTokenSpan);
                        break;

                    case JsonBinaryEncoding.TypeMarker.StrL4:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.FourByteLength)
                        {
                            value = default;
                            return false;
                        }

                        start = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength;
                        length = MemoryMarshal.Read<uint>(stringTokenSpan);
                        break;

                    case JsonBinaryEncoding.TypeMarker.StrR1:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.OneByteOffset)
                        {
                            value = default;
                            return false;
                        }

                        return TryGetBufferedStringValue(
                            buffer,
                            buffer.Slice(start: stringTokenSpan[0]),
                            out value);

                    case JsonBinaryEncoding.TypeMarker.StrR2:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.TwoByteOffset)
                        {
                            value = default;
                            return false;
                        }

                        return TryGetBufferedStringValue(
                            buffer,
                            buffer.Slice(start: MemoryMarshal.Read<ushort>(stringTokenSpan)),
                            out value);

                    case JsonBinaryEncoding.TypeMarker.StrR3:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.ThreeByteOffset)
                        {
                            value = default;
                            return false;
                        }

                        return TryGetBufferedStringValue(
                            buffer,
                            buffer.Slice(start: MemoryMarshal.Read<UInt24>(stringTokenSpan)),
                            out value);

                    case JsonBinaryEncoding.TypeMarker.StrR4:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.FourByteOffset)
                        {
                            value = default;
                            return false;
                        }

                        return TryGetBufferedStringValue(
                            buffer,
                            buffer.Slice(start: (int)MemoryMarshal.Read<uint>(stringTokenSpan)),
                            out value);

                    default:
                        value = default;
                        return false;
                }

                if ((start + length) > stringToken.Length)
                {
                    value = default;
                    return false;
                }
            }

            value = Utf8Memory.UnsafeCreateNoValidation(stringToken.Slice(start: start, length: (int)length));
            return true;
        }

        /// <summary>
        /// Try Get Encoded String Type Marker
        /// </summary>
        /// <param name="utf8Span">the value</param>
        /// <param name="multiByteTypeMarker">The encoded string type marker if found.</param>
        /// <returns>Whether or not the type marker was found.</returns>
        public static bool TryGetEncodedStringTypeMarker(
            Utf8Span utf8Span,
            out MultiByteTypeMarker multiByteTypeMarker)
        {
            if (JsonBinaryEncoding.TryGetEncodedSystemStringTypeMarker(utf8Span, out multiByteTypeMarker))
            {
                return true;
            }

            if (JsonBinaryEncoding.TryGetEncodedUserStringTypeMarker(utf8Span, out multiByteTypeMarker))
            {
                return true;
            }

            multiByteTypeMarker = default;
            return false;
        }

        /// <summary>
        /// Try Get Encoded System String Type Marker
        /// </summary>
        /// <param name="utf8Span">The value.</param>
        /// <param name="multiByteTypeMarker">The multi byte type marker if found.</param>
        /// <returns>Whether or not the Encoded System String Type Marker was found.</returns>
        private static bool TryGetEncodedSystemStringTypeMarker(
            Utf8Span utf8Span,
            out MultiByteTypeMarker multiByteTypeMarker)
        {
            if (JsonBinaryEncoding.SystemStrings.TryGetSystemStringId(utf8Span, out int systemStringId))
            {
                multiByteTypeMarker = new MultiByteTypeMarker(
                    length: 1,
                    one: (byte)(TypeMarker.SystemString1ByteLengthMin + systemStringId));

                return true;
            }

            multiByteTypeMarker = default;
            return false;
        }

        /// <summary>
        /// Try Get Encoded User String Type Marker
        /// </summary>
        /// <param name="utf8Span">The value.</param>
        /// <param name="multiByteTypeMarker">The multi byte type marker if found.</param>
        /// <returns>Whether or not the Encoded User String Type Marker was found.</returns>
        private static bool TryGetEncodedUserStringTypeMarker(
            Utf8Span utf8Span,
            out MultiByteTypeMarker multiByteTypeMarker)
        {
            multiByteTypeMarker = default;
            return false;
        }

        [Flags]
        private enum EncodeGuidParseFlags
        {
            None = 0x0,
            LowerCase = 0x1,
            UpperCase = 0x2,
            Invalid = 0xFF,
        }

        public static bool TryEncodeGuidString(ReadOnlySpan<byte> guidString, Span<byte> destinationBuffer)
        {
            if (guidString.Length < GuidLength)
            {
                return false;
            }

            if (destinationBuffer.Length < EncodedGuidLength)
            {
                return false;
            }

            EncodeGuidParseFlags flags = EncodeGuidParseFlags.None;
            Span<byte> writePointer = destinationBuffer.Slice(start: 1);

            int dashIndex = 8;
            int oddEven = 0;

            for (int index = 0; index < GuidLength; index++)
            {
                char c = (char)guidString[index];

                if ((index == dashIndex) && (index <= 23))
                {
                    if (c != '-')
                    {
                        flags = EncodeGuidParseFlags.Invalid;
                        break;
                    }

                    dashIndex += 5;
                    oddEven = oddEven == 0 ? 1 : 0;
                    continue;
                }

                byte value;
                if ((c >= '0') && (c <= '9'))
                {
                    value = (byte)(c - '0');
                }
                else if ((c >= 'a') && (c <= 'f'))
                {
                    value = (byte)(10 + c - 'a');
                    flags |= EncodeGuidParseFlags.LowerCase;
                }
                else if ((c >= 'A') && (c <= 'F'))
                {
                    value = (byte)(10 + c - 'A');
                    flags |= EncodeGuidParseFlags.UpperCase;
                }
                else
                {
                    flags = EncodeGuidParseFlags.Invalid;
                    break;
                }

                if ((index % 2) == oddEven)
                {
                    writePointer[0] = value;
                }
                else
                {
                    writePointer[0] = (byte)(writePointer[0] | (value << 4));
                    writePointer = writePointer.Slice(start: 1);
                }
            }

            // Set the type marker
            if ((flags == EncodeGuidParseFlags.None) || (flags == EncodeGuidParseFlags.LowerCase))
            {
                destinationBuffer[0] = JsonBinaryEncoding.TypeMarker.LowercaseGuidString;
            }
            else if (flags == EncodeGuidParseFlags.UpperCase)
            {
                destinationBuffer[0] = JsonBinaryEncoding.TypeMarker.UppercaseGuidString;
            }
            else
            {
                return false;
            }

            return true;
        }

        public static bool TryEncodeCompressedString(
            ReadOnlySpan<byte> stringValue,
            Span<byte> destinationBuffer,
            out int bytesWritten)
        {
            if (destinationBuffer.Length < MinCompressedStringLength)
            {
                bytesWritten = default;
                return false;
            }

            int firstSetBit = 128;
            int lastSetBit = 0;
            int charCount = 0;
            BitArray valueCharSet = new BitArray(length: 128);
            // Create a bit-set with all the ASCII character of the string value
            for (int index = 0; index < stringValue.Length; index++)
            {
                byte charValue = stringValue[index];

                // Only ASCII characters
                if (charValue >= 128)
                {
                    bytesWritten = default;
                    return false;
                }

                if (!valueCharSet[charValue])
                {
                    charCount++;

                    firstSetBit = Math.Min(charValue, firstSetBit);
                    lastSetBit = Math.Max(charValue, lastSetBit);
                }

                valueCharSet.Set(charValue, true);
            }

            int charRange = (lastSetBit - firstSetBit) + 1;

            // Attempt to encode the string as 4-bit packed values over a defined character set
            if ((stringValue.Length <= 0xFF) && (charCount <= 16) && (stringValue.Length >= Min4BitCharSetStringLength))
            {
                // DateTime character set
                if (valueCharSet.IsSubset(StringCompressionLookupTables.DateTime.Bitmap))
                {
                    return TryEncodeString(JsonBinaryEncoding.TypeMarker.CompressedDateTimeString, stringValue, baseChar: 0, destinationBuffer, out bytesWritten);
                }

                // Lowercase hexadecimal character set
                if (valueCharSet.IsSubset(StringCompressionLookupTables.LowercaseHex.Bitmap))
                {
                    return TryEncodeString(JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString, stringValue, baseChar: 0, destinationBuffer, out bytesWritten);
                }

                // Uppercase hexadecimal character set
                if (valueCharSet.IsSubset(StringCompressionLookupTables.UppercaseHex.Bitmap))
                {
                    return TryEncodeString(JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString, stringValue, baseChar: 0, destinationBuffer, out bytesWritten);
                }
            }

            // Attempt to encode the string as n-bits packed characters with a base character
            if (stringValue.Length <= 0xFF)
            {
                // 4-bit packed characters
                if ((charRange <= 16) && (stringValue.Length >= MinCompressedStringLength4))
                {
                    return TryEncodeString(JsonBinaryEncoding.TypeMarker.Packed4BitString, stringValue, baseChar: (byte)firstSetBit, destinationBuffer, out bytesWritten);
                }

                // 5-bit packed characters
                if ((charRange <= 32) && (stringValue.Length >= MinCompressedStringLength5))
                {
                    return TryEncodeString(JsonBinaryEncoding.TypeMarker.Packed5BitString, stringValue, baseChar: (byte)firstSetBit, destinationBuffer, out bytesWritten);
                }

                // 6-bit packed characters
                if ((charRange <= 64) && (stringValue.Length >= MinCompressedStringLength6))
                {
                    return TryEncodeString(JsonBinaryEncoding.TypeMarker.Packed6BitString, stringValue, baseChar: (byte)firstSetBit, destinationBuffer, out bytesWritten);
                }
            }

            // Try encode the string as 7 bit packed characters with no base value
            if (stringValue.Length >= MinCompressedStringLength7)
            {
                // 1 byte length
                if (stringValue.Length <= 0xFF)
                {
                    return TryEncodeString(JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1, stringValue, baseChar: 0, destinationBuffer, out bytesWritten);
                }

                // 2 byte length
                if (stringValue.Length <= 0xFFFF)
                {
                    return TryEncodeString(JsonBinaryEncoding.TypeMarker.Packed7BitStringLength2, stringValue, baseChar: 0, destinationBuffer, out bytesWritten);
                }
            }

            bytesWritten = default;
            return false;
        }

        private static bool TryEncodeString(byte typeMarker, ReadOnlySpan<byte> stringValue, byte baseChar, Span<byte> destinationBuffer, out int bytesWritten)
        {
            bool isHexadecimalString = JsonBinaryEncoding.TypeMarker.IsHexadecimalString(typeMarker);
            bool isDateTimeString = JsonBinaryEncoding.TypeMarker.IsDateTimeString(typeMarker);
            bool isCompressedString = JsonBinaryEncoding.TypeMarker.IsCompressedString(typeMarker);

            if (!(isHexadecimalString || isDateTimeString || isCompressedString))
            {
                throw new ArgumentException("typeMarker must be a hexadecimal, datetime, or compressed string");
            }

            int lengthByteCount = (isHexadecimalString || isDateTimeString) ? 1 : (isCompressedString ? ((typeMarker == JsonBinaryEncoding.TypeMarker.Packed7BitStringLength2) ? 2 : 1) : 0);
            int baseCharByteCount = JsonBinaryEncoding.TypeMarker.InRange(typeMarker, JsonBinaryEncoding.TypeMarker.Packed4BitString, JsonBinaryEncoding.TypeMarker.Packed6BitString + 1) ? 1 : 0;
            int prefixByteCount = 1 + lengthByteCount + baseCharByteCount;
            int numberOfBits = (isHexadecimalString || isDateTimeString) ? 4 : (typeMarker >= JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1) ? 7 : (4 + typeMarker - JsonBinaryEncoding.TypeMarker.Packed4BitString);

            int encodedLength = JsonBinaryEncoding.ValueLengths.GetCompressedStringLength(stringValue.Length, numberOfBits);
            int bufferLength = prefixByteCount + encodedLength;

            if (!destinationBuffer.IsEmpty)
            {
                if (destinationBuffer.Length < bufferLength)
                {
                    throw new ArgumentException($"{nameof(destinationBuffer)} is too small.");
                }

                // Write the typemarker
                destinationBuffer[0] = typeMarker;
                destinationBuffer = destinationBuffer.Slice(start: 1);

                // Write the string length
                if (lengthByteCount == 1)
                {
                    destinationBuffer[0] = (byte)stringValue.Length;
                    destinationBuffer = destinationBuffer.Slice(start: 1);
                }
                else
                {
                    SetFixedSizedValue<ushort>(destinationBuffer, (ushort)stringValue.Length);
                    destinationBuffer = destinationBuffer.Slice(start: 2);
                }

                // Write base char
                if (baseCharByteCount == 1)
                {
                    destinationBuffer[0] = (byte)baseChar;
                    destinationBuffer = destinationBuffer.Slice(start: 1);
                }

                // trim the destination buffer to be only the encoded bytes
                destinationBuffer = destinationBuffer.Slice(start: 0, encodedLength);

                EncodeStringValue(typeMarker, stringValue, baseChar, destinationBuffer);
            }

            bytesWritten = bufferLength;
            return true;
        }

        private static void EncodeStringValue(byte typeMarker, ReadOnlySpan<byte> stringValue, byte baseChar, Span<byte> destinationBuffer)
        {
            switch (typeMarker)
            {
                case TypeMarker.CompressedLowercaseHexString:
                    Encode4BitCharacterStringValue(StringCompressionLookupTables.LowercaseHex, stringValue, destinationBuffer);
                    break;
                case TypeMarker.CompressedUppercaseHexString:
                    Encode4BitCharacterStringValue(StringCompressionLookupTables.UppercaseHex, stringValue, destinationBuffer);
                    break;
                case TypeMarker.CompressedDateTimeString:
                    Encode4BitCharacterStringValue(StringCompressionLookupTables.DateTime, stringValue, destinationBuffer);
                    break;

                case TypeMarker.Packed4BitString:
                    EncodeCompressedStringValue(numberOfBits: 4, stringValue, baseChar, destinationBuffer);
                    break;
                case TypeMarker.Packed5BitString:
                    EncodeCompressedStringValue(numberOfBits: 5, stringValue, baseChar, destinationBuffer);
                    break;
                case TypeMarker.Packed6BitString:
                    EncodeCompressedStringValue(numberOfBits: 6, stringValue, baseChar, destinationBuffer);
                    break;
                case TypeMarker.Packed7BitStringLength1:
                case TypeMarker.Packed7BitStringLength2:
                    EncodeCompressedStringValue(numberOfBits: 7, stringValue, baseChar, destinationBuffer);
                    break;
                default:
                    throw new ArgumentOutOfRangeException($"Unknown {nameof(typeMarker)} {typeMarker}.");
            }
        }

        private static void Encode4BitCharacterStringValue(StringCompressionLookupTables chars, ReadOnlySpan<byte> stringValue, Span<byte> destinationBuffer)
        {
            for (int index = 0; index < stringValue.Length; index++)
            {
                byte c = stringValue[index];

                byte value = chars.CharToByte[c];

                if ((index % 2) == 0)
                {
                    destinationBuffer[0] = value;
                }
                else
                {
                    destinationBuffer[0] = (byte)(destinationBuffer[0] | (value << 4));
                    destinationBuffer = destinationBuffer.Slice(start: 1);
                }
            }
        }

        private static void EncodeCompressedStringValue(int numberOfBits, ReadOnlySpan<byte> stringValue, byte baseChar, Span<byte> destinationBuffer)
        {
            Span<ulong> packedValue = stackalloc ulong[1];
            int index = 0;

            for (; index < stringValue.Length / 8 * 8; index += 8)
            {
                packedValue[0] = (((ulong)stringValue[index + 0]) - baseChar) << (0 * numberOfBits);
                packedValue[0] |= (((ulong)stringValue[index + 1]) - baseChar) << (1 * numberOfBits);
                packedValue[0] |= (((ulong)stringValue[index + 2]) - baseChar) << (2 * numberOfBits);
                packedValue[0] |= (((ulong)stringValue[index + 3]) - baseChar) << (3 * numberOfBits);
                packedValue[0] |= (((ulong)stringValue[index + 4]) - baseChar) << (4 * numberOfBits);
                packedValue[0] |= (((ulong)stringValue[index + 5]) - baseChar) << (5 * numberOfBits);
                packedValue[0] |= (((ulong)stringValue[index + 6]) - baseChar) << (6 * numberOfBits);
                packedValue[0] |= (((ulong)stringValue[index + 7]) - baseChar) << (7 * numberOfBits);

                Span<byte> packedValueAsBytes = MemoryMarshal.AsBytes(packedValue);
                packedValueAsBytes.Slice(start: 0, length: numberOfBits).CopyTo(destinationBuffer);
                destinationBuffer = destinationBuffer.Slice(start: numberOfBits);
            }

            if (index < stringValue.Length)
            {
                Span<byte> paddedStringValue = stackalloc byte[8];
                Span<byte> encodedPaddedStringValue = stackalloc byte[8];
                stringValue.Slice(start: index).CopyTo(paddedStringValue);
                EncodeCompressedStringValue(numberOfBits, paddedStringValue, baseChar, encodedPaddedStringValue);
                encodedPaddedStringValue.Slice(start: 0, length: destinationBuffer.Length).CopyTo(destinationBuffer);
            }
        }

        private static bool EncodedStringEqualsTo(byte typeMarker, ReadOnlySpan<byte> encodedStringValue, ReadOnlySpan<byte> stringValue)
        {
            if (encodedStringValue.Length != GetEncodedStringValueLength(stringValue))
            {
                return false;
            }

            switch (typeMarker)
            {
                case JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString:
                case JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString:
                case JsonBinaryEncoding.TypeMarker.CompressedDateTimeString:
                    {
                        encodedStringValue = encodedStringValue.Slice(start: 2);

                        Span<byte> buffer = stackalloc byte[8];
                        int index = 0;
                        for (; index < stringValue.Length / 8 * 8; index++)
                        {
                            if (typeMarker == JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString)
                            {
                                Decode4BitCharacterStringValue(StringCompressionLookupTables.LowercaseHex, encodedStringValue, buffer);
                            }
                            else if (typeMarker == JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString)
                            {
                                Decode4BitCharacterStringValue(StringCompressionLookupTables.UppercaseHex, encodedStringValue, buffer);
                            }
                            else
                            {
                                Decode4BitCharacterStringValue(StringCompressionLookupTables.DateTime, encodedStringValue, buffer);
                            }

                            if (!buffer.SequenceEqual(stringValue.Slice(start: 0, length: 8)))
                            {
                                return false;
                            }

                            stringValue = stringValue.Slice(start: 8);
                            encodedStringValue = encodedStringValue.Slice(start: 4);
                        }

                        if (index < stringValue.Length)
                        {
                            Span<byte> input = stackalloc byte[8];
                            encodedStringValue.CopyTo(input);

                            if (typeMarker == JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString)
                            {
                                Decode4BitCharacterStringValue(StringCompressionLookupTables.LowercaseHex, input, buffer);
                            }
                            else if (typeMarker == JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString)
                            {
                                Decode4BitCharacterStringValue(StringCompressionLookupTables.UppercaseHex, input, buffer);
                            }
                            else
                            {
                                Decode4BitCharacterStringValue(StringCompressionLookupTables.DateTime, input, buffer);
                            }

                            return input.SequenceEqual(stringValue.Slice(start: index));
                        }

                        return true;
                    }

                case JsonBinaryEncoding.TypeMarker.Packed4BitString:
                case JsonBinaryEncoding.TypeMarker.Packed5BitString:
                case JsonBinaryEncoding.TypeMarker.Packed6BitString:
                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1:
                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength2:
                    {
                        byte numberOfBits = typeMarker switch
                        {
                            JsonBinaryEncoding.TypeMarker.Packed4BitString => 4,
                            JsonBinaryEncoding.TypeMarker.Packed5BitString => 5,
                            JsonBinaryEncoding.TypeMarker.Packed6BitString => 6,
                            _ => 7,
                        };

                        byte baseChar = GetEncodedStringBaseChar(stringValue);

                        encodedStringValue = encodedStringValue.Slice(start: (typeMarker == JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1) ? 2 : 3);

                        Span<byte> buffer = stackalloc byte[8];
                        int index = 0;
                        for (; index < stringValue.Length / 8 * 8; index++)
                        {
                            DecodeCompressedStringValue(numberOfBits, encodedStringValue, baseChar, buffer);

                            if (!buffer.SequenceEqual(stringValue.Slice(start: 0, length: 8)))
                            {
                                return false;
                            }

                            stringValue = stringValue.Slice(start: 8);
                            encodedStringValue = encodedStringValue.Slice(start: numberOfBits);
                        }

                        if (index < stringValue.Length)
                        {
                            Span<byte> input = stackalloc byte[8];
                            encodedStringValue.CopyTo(input);
                            DecodeCompressedStringValue(numberOfBits, input, baseChar, buffer);

                            return buffer.SequenceEqual(stringValue.Slice(start: 0, length: 8));
                        }

                        return true;
                    }

                case JsonBinaryEncoding.TypeMarker.LowercaseGuidString:
                case JsonBinaryEncoding.TypeMarker.UppercaseGuidString:
                    {
                        Span<byte> guidBuffer = stackalloc byte[GuidLength];
                        DecodeGuidStringValue(encodedStringValue, isUpperCaseGuid: typeMarker == JsonBinaryEncoding.TypeMarker.UppercaseGuidString, guidBuffer);
                        return guidBuffer.SequenceEqual(stringValue);
                    }

                case JsonBinaryEncoding.TypeMarker.DoubleQuotedLowercaseGuidString:
                    {
                        if ((stringValue[0] != '"') || (stringValue[GuidWithQuotesLength - 1] != '"'))
                        {
                            return false;
                        }

                        Span<byte> guidBuffer = stackalloc byte[GuidLength];
                        DecodeGuidStringValue(encodedStringValue, isUpperCaseGuid: false, guidBuffer);
                        return guidBuffer.SequenceEqual(stringValue.Slice(start: 1, length: stringValue.Length - 2));
                    }

                default:
                    throw new ArgumentOutOfRangeException($"Unrecognized {nameof(typeMarker)}: {typeMarker}.");
            }
        }

        private static void DecodeString(ReadOnlySpan<byte> stringToken, Span<byte> destinationBuffer, out int bytesWritten)
        {
            byte typeMarker = stringToken[0];

            bool isHexadecimalString = JsonBinaryEncoding.TypeMarker.IsHexadecimalString(typeMarker);
            bool isDateTimeString = JsonBinaryEncoding.TypeMarker.IsDateTimeString(typeMarker);
            bool isCompressedString = JsonBinaryEncoding.TypeMarker.IsCompressedString(typeMarker);
            bool isGuidString = JsonBinaryEncoding.TypeMarker.IsGuidString(typeMarker);

            if (!(isHexadecimalString || isDateTimeString || isCompressedString || isGuidString))
            {
                throw new ArgumentException("token must be a hex, datetime, compressed, or guid string.");
            }

            int lengthByteCount = (isHexadecimalString || isDateTimeString) ? 1 : (isCompressedString ? ((typeMarker == JsonBinaryEncoding.TypeMarker.Packed7BitStringLength2) ? 2 : 1) : 0);
            int baseCharByteCount = JsonBinaryEncoding.TypeMarker.InRange(typeMarker, JsonBinaryEncoding.TypeMarker.Packed4BitString, TypeMarker.Packed6BitString + 1) ? 1 : 0;
            int prefixByteCount = TypeMarkerLength + lengthByteCount + baseCharByteCount;

            if (stringToken.Length < prefixByteCount)
            {
                throw new JsonInvalidTokenException();
            }

            bytesWritten = GetEncodedStringValueLength(stringToken);
            int encodedLength = GetEncodedStringBufferLength(stringToken);
            byte baseChar = GetEncodedStringBaseChar(stringToken);

            if (stringToken.Length < (prefixByteCount + encodedLength))
            {
                throw new JsonInvalidTokenException();
            }

            if (!destinationBuffer.IsEmpty)
            {
                if (bytesWritten > destinationBuffer.Length)
                {
                    throw new InvalidOperationException("buffer is too small");
                }

                ReadOnlySpan<byte> encodedString = stringToken.Slice(start: prefixByteCount, length: encodedLength);

                DecodeStringValue(typeMarker, encodedString, baseChar, destinationBuffer.Slice(start: 0, length: bytesWritten));
            }
        }

        private static int GetEncodedStringValueLength(ReadOnlySpan<byte> stringToken)
        {
            byte typeMarker = stringToken[0];

            switch (typeMarker)
            {
                case JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString:
                case JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString:
                case JsonBinaryEncoding.TypeMarker.CompressedDateTimeString:
                case JsonBinaryEncoding.TypeMarker.Packed4BitString:
                case JsonBinaryEncoding.TypeMarker.Packed5BitString:
                case JsonBinaryEncoding.TypeMarker.Packed6BitString:
                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1:
                    return stringToken[1];

                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength2:
                    return GetFixedSizedValue<ushort>(stringToken.Slice(start: 1));

                case JsonBinaryEncoding.TypeMarker.LowercaseGuidString:
                case JsonBinaryEncoding.TypeMarker.UppercaseGuidString:
                    return GuidLength;

                case JsonBinaryEncoding.TypeMarker.DoubleQuotedLowercaseGuidString:
                    return GuidWithQuotesLength;

                default:
                    throw new ArgumentOutOfRangeException($"Unexpected type marker: {typeMarker}.");
            }
        }

        private static int GetEncodedStringBufferLength(ReadOnlySpan<byte> stringToken)
        {
            byte typeMarker = stringToken[0];

            switch (typeMarker)
            {
                case JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString:
                case JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString:
                case JsonBinaryEncoding.TypeMarker.CompressedDateTimeString:
                    return JsonBinaryEncoding.ValueLengths.GetCompressedStringLength(stringToken[1], numberOfBits: 4);

                case JsonBinaryEncoding.TypeMarker.Packed4BitString:
                    return JsonBinaryEncoding.ValueLengths.GetCompressedStringLength(stringToken[1], numberOfBits: 4);

                case JsonBinaryEncoding.TypeMarker.Packed5BitString:
                    return JsonBinaryEncoding.ValueLengths.GetCompressedStringLength(stringToken[1], numberOfBits: 5);

                case JsonBinaryEncoding.TypeMarker.Packed6BitString:
                    return JsonBinaryEncoding.ValueLengths.GetCompressedStringLength(stringToken[1], numberOfBits: 6);

                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1:
                    return JsonBinaryEncoding.ValueLengths.GetCompressedStringLength(stringToken[1], numberOfBits: 7);

                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength2:
                    return JsonBinaryEncoding.ValueLengths.GetCompressedStringLength(GetFixedSizedValue<ushort>(stringToken.Slice(1)), numberOfBits: 7);

                case JsonBinaryEncoding.TypeMarker.LowercaseGuidString:
                case JsonBinaryEncoding.TypeMarker.UppercaseGuidString:
                case JsonBinaryEncoding.TypeMarker.DoubleQuotedLowercaseGuidString:
                    return 16;

                default:
                    throw new ArgumentException($"Invalid type marker: {typeMarker}");
            }
        }

        private static byte GetEncodedStringBaseChar(ReadOnlySpan<byte> stringToken)
        {
            byte typeMarker = stringToken[0];

            switch (typeMarker)
            {
                case JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString:
                case JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString:
                case JsonBinaryEncoding.TypeMarker.CompressedDateTimeString:
                    return 0;

                case JsonBinaryEncoding.TypeMarker.Packed4BitString:
                case JsonBinaryEncoding.TypeMarker.Packed5BitString:
                case JsonBinaryEncoding.TypeMarker.Packed6BitString:
                    return stringToken[2];

                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1:
                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength2:
                    return 0;

                case JsonBinaryEncoding.TypeMarker.LowercaseGuidString:
                case JsonBinaryEncoding.TypeMarker.UppercaseGuidString:
                case JsonBinaryEncoding.TypeMarker.DoubleQuotedLowercaseGuidString:
                    return 0;

                default:
                    throw new ArgumentException($"Invalid type marker: {typeMarker}");
            }
        }

        private static void DecodeStringValue(byte typeMarker, ReadOnlySpan<byte> encodedString, byte baseChar, Span<byte> destinationBuffer)
        {
            switch (typeMarker)
            {
                case JsonBinaryEncoding.TypeMarker.CompressedLowercaseHexString:
                    if (baseChar != 0)
                    {
                        throw new InvalidOperationException("base char needs to be 0.");
                    }

                    Decode4BitCharacterStringValue(StringCompressionLookupTables.LowercaseHex, encodedString, destinationBuffer);
                    break;

                case JsonBinaryEncoding.TypeMarker.CompressedUppercaseHexString:
                    if (baseChar != 0)
                    {
                        throw new InvalidOperationException("base char needs to be 0.");
                    }

                    Decode4BitCharacterStringValue(StringCompressionLookupTables.UppercaseHex, encodedString, destinationBuffer);
                    break;

                case JsonBinaryEncoding.TypeMarker.CompressedDateTimeString:
                    if (baseChar != 0)
                    {
                        throw new InvalidOperationException("base char needs to be 0.");
                    }

                    Decode4BitCharacterStringValue(StringCompressionLookupTables.DateTime, encodedString, destinationBuffer);
                    break;

                case JsonBinaryEncoding.TypeMarker.Packed4BitString:
                    DecodeCompressedStringValue(numberOfBits: 4, encodedString, baseChar, destinationBuffer);
                    break;

                case JsonBinaryEncoding.TypeMarker.Packed5BitString:
                    DecodeCompressedStringValue(numberOfBits: 5, encodedString, baseChar, destinationBuffer);
                    break;

                case JsonBinaryEncoding.TypeMarker.Packed6BitString:
                    DecodeCompressedStringValue(numberOfBits: 6, encodedString, baseChar, destinationBuffer);
                    break;

                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength1:
                case JsonBinaryEncoding.TypeMarker.Packed7BitStringLength2:
                    if (baseChar != 0)
                    {
                        throw new InvalidOperationException("base char needs to be 0.");
                    }

                    DecodeCompressedStringValue(numberOfBits: 7, encodedString, baseChar, destinationBuffer);
                    break;

                case JsonBinaryEncoding.TypeMarker.LowercaseGuidString:
                case JsonBinaryEncoding.TypeMarker.UppercaseGuidString:
                    DecodeGuidStringValue(encodedString, isUpperCaseGuid: typeMarker == JsonBinaryEncoding.TypeMarker.UppercaseGuidString, destinationBuffer);
                    break;

                case JsonBinaryEncoding.TypeMarker.DoubleQuotedLowercaseGuidString:
                    destinationBuffer[0] = (byte)'"';
                    DecodeGuidStringValue(encodedString, isUpperCaseGuid: false, destinationBuffer.Slice(start: 1));
                    destinationBuffer[GuidWithQuotesLength - 1] = (byte)'"';
                    break;

                default:
                    throw new JsonInvalidTokenException();
            }
        }

        private static void Decode4BitCharacterStringValue(
            StringCompressionLookupTables chars,
            ReadOnlySpan<byte> encodedString,
            Span<byte> destinationBuffer)
        {
            if (encodedString.Length != JsonBinaryEncoding.ValueLengths.GetCompressedStringLength(destinationBuffer.Length, numberOfBits: 4))
            {
                throw new ArgumentException("destination buffer is too small.");
            }

            ReadOnlySpan<byte> encodedStringBuffer = encodedString;
            Span<ushort> destinationBufferAsTwoByteChars = MemoryMarshal.Cast<byte, ushort>(destinationBuffer);

            for (int index = 0; index < destinationBuffer.Length / 2; index++)
            {
                ushort value = chars.ByteToTwoChars[encodedStringBuffer[index]];
                destinationBufferAsTwoByteChars[index] = value;
            }

            if ((destinationBuffer.Length % 2) == 1)
            {
                if (encodedStringBuffer[encodedStringBuffer.Length - 1] > 0x0F)
                {
                    throw new InvalidOperationException();
                }

                destinationBuffer[destinationBuffer.Length - 1] = (byte)chars.List[encodedString[encodedString.Length - 1]];
            }
        }

        private static void DecodeCompressedStringValue(
            int numberOfBits,
            ReadOnlySpan<byte> encodedString,
            byte baseChar,
            Span<byte> destinationBuffer)
        {
            if (numberOfBits > 8 || numberOfBits < 0)
            {
                throw new ArgumentException("Invalid number of bits.");
            }

            long mask = 0x000000FF >> (8 - numberOfBits);
            int index = 0;
            Span<byte> packedValueByteArray = stackalloc byte[8];
            int iterations = destinationBuffer.Length / 8 * 8;
            for (; index < iterations; index += 8)
            {
                encodedString.Slice(start: 0, length: numberOfBits).CopyTo(packedValueByteArray);

                long packedValue = MemoryMarshal.Cast<byte, long>(packedValueByteArray)[0];

                destinationBuffer[0] = (byte)(((byte)(packedValue & mask)) + baseChar);
                packedValue >>= numberOfBits;

                destinationBuffer[1] = (byte)(((byte)(packedValue & mask)) + baseChar);
                packedValue >>= numberOfBits;

                destinationBuffer[2] = (byte)(((byte)(packedValue & mask)) + baseChar);
                packedValue >>= numberOfBits;

                destinationBuffer[3] = (byte)(((byte)(packedValue & mask)) + baseChar);
                packedValue >>= numberOfBits;

                destinationBuffer[4] = (byte)(((byte)(packedValue & mask)) + baseChar);
                packedValue >>= numberOfBits;

                destinationBuffer[5] = (byte)(((byte)(packedValue & mask)) + baseChar);
                packedValue >>= numberOfBits;

                destinationBuffer[6] = (byte)(((byte)(packedValue & mask)) + baseChar);
                packedValue >>= numberOfBits;

                destinationBuffer[7] = (byte)(((byte)(packedValue & mask)) + baseChar);
                packedValue >>= numberOfBits;

                if (packedValue != 0)
                {
                    throw new InvalidOperationException();
                }

                encodedString = encodedString.Slice(start: numberOfBits);
                destinationBuffer = destinationBuffer.Slice(start: 8);
            }

            if (!destinationBuffer.IsEmpty)
            {
                Span<byte> paddedString = stackalloc byte[8];
                Span<byte> decodedPaddedString = stackalloc byte[8];
                encodedString.CopyTo(paddedString);
                DecodeCompressedStringValue(numberOfBits, paddedString, baseChar, decodedPaddedString);
                decodedPaddedString.Slice(start: 0, length: destinationBuffer.Length).CopyTo(destinationBuffer);
            }
        }

        private static void DecodeGuidStringValue(ReadOnlySpan<byte> encodedString, bool isUpperCaseGuid, Span<byte> destinationBuffer)
        {
            ImmutableArray<ushort> byteLookupTable = isUpperCaseGuid ? StringCompressionLookupTables.UppercaseHex.ByteToTwoChars : StringCompressionLookupTables.LowercaseHex.ByteToTwoChars;

            // GUID Format: XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 0), byteLookupTable[encodedString[0]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 2), byteLookupTable[encodedString[1]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 4), byteLookupTable[encodedString[2]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 6), byteLookupTable[encodedString[3]]);
            destinationBuffer[8] = (byte)'-';
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 9), byteLookupTable[encodedString[4]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 11), byteLookupTable[encodedString[5]]);
            destinationBuffer[13] = (byte)'-';
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 14), byteLookupTable[encodedString[6]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 16), byteLookupTable[encodedString[7]]);
            destinationBuffer[18] = (byte)'-';
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 19), byteLookupTable[encodedString[8]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 21), byteLookupTable[encodedString[9]]);
            destinationBuffer[23] = (byte)'-';
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 24), byteLookupTable[encodedString[10]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 26), byteLookupTable[encodedString[11]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 28), byteLookupTable[encodedString[12]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 30), byteLookupTable[encodedString[13]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 32), byteLookupTable[encodedString[14]]);
            SetFixedSizedValue<ushort>(destinationBuffer.Slice(start: 34), byteLookupTable[encodedString[15]]);
        }
    }
}
