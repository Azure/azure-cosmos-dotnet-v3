// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Immutable;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;

    internal static partial class JsonBinaryEncoding
    {
        private static class ValueLengths
        {
            private const int L1 = -1;           // 1-byte length
            private const int L2 = -2;           // 2-byte length
            private const int L4 = -3;           // 4-byte length
            private const int LC1 = -4;          // 1-byte length followed by 1-byte count
            private const int LC2 = -5;          // 2-byte length followed by 2-byte count
            private const int LC4 = -6;          // 4-byte length followed by 4-byte count
            private const int CS4L1 = -7;        // 4-bit Compressed string w/ 1-byte length
            private const int CS7L1 = -8;        // 7-bit Compressed string w/ 1-byte length
            private const int CS7L2 = -9;        // 7-bit Compressed string w/ 2-byte length
            private const int CS4BL1 = -10;      // 4-bit Compressed string w/ 1-byte length followed by 1-byte base char
            private const int CS5BL1 = -11;      // 5-bit Compressed string w/ 1-byte length followed by 1-byte base char
            private const int CS6BL1 = -12;      // 6-bit Compressed string w/ 1-byte length followed by 1-byte base char
            private const int Arr1 = -13;        // 1-item array
            private const int Obj1 = -14;        // 1-property object
            private const int NC1 = -15;         // Fixed-size numeric items of 1-byte item count
            private const int NC2 = -16;         // Fixed-size numeric items of 2-byte item count
            private const int ANC1 = -17;        // Array of fixed-size numeric items of 1-byte item count
            private const int ANC2 = -18;        // Array of fixed-size numeric items of 2-byte item count

            /// <summary>
            /// Lookup table for encoded value length for each TypeMarker value (0 to 255)
            /// The lengths are encoded as follows:
            /// - Positive Value: The length of the value including its TypeMarker
            /// - Negative Value: The length is encoded as an integer of size equals to abs(value) following the TypeMarker byte
            /// - Zero Value: The length is unknown (for instance an unassigned type marker)
            /// </summary>
            public static readonly ImmutableArray<int> Lookup = new int[256]
            {
                // Encoded literal integer value (32 values)
                1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1,

                // Encoded 1-byte system string (32 values)
                1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1,

                // Encoded 1-byte user string (32 values)
                1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1,
    
                // Encoded 2-byte user string (8 values)
                2, 2, 2, 2, 2, 2, 2, 2,

                // String Values [0x68, 0x70)
                0,      // <empty> 0x68
                0,      // <empty> 0x69
                0,      // <empty> 0x6A
                0,      // <empty> 0x6B
                0,      // <empty> 0x6C
                0,      // <empty> 0x6D
                0,      // <empty> 0x6E
                0,      // <empty> 0x6F

                // String Values [0x70, 0x78)
                0,      // <empty> 0x70
                0,      // <empty> 0x71
                0,      // <empty> 0x72
                0,      // <empty> 0x73
                0,      // <empty> 0x74
                17,     // StrGL (Lowercase GUID string)
                17,     // StrGU (Uppercase GUID string)
                17,     // StrGQ (Double-quoted lowercase GUID string)

                // Compressed strings [0x78, 0x80)
                CS4L1,  // String 1-byte length - Lowercase hexadecimal digits encoded as 4-bit characters
                CS4L1,  // String 1-byte length - Uppercase hexadecimal digits encoded as 4-bit characters
                CS4L1,  // String 1-byte length - Date-time character set encoded as 4-bit characters
                CS4BL1, // String 1-byte Length - 4-bit packed characters relative to a base value
                CS5BL1, // String 1-byte Length - 5-bit packed characters relative to a base value
                CS6BL1, // String 1-byte Length - 6-bit packed characters relative to a base value
                CS7L1,  // String 1-byte Length - 7-bit packed characters
                CS7L2,  // String 2-byte Length - 7-bit packed characters

                // TypeMarker-encoded string length (64 values)
                1, 2, 3, 4, 5, 6, 7, 8,
                9, 10, 11, 12, 13, 14, 15, 16,
                17, 18, 19, 20, 21, 22, 23, 24,
                25, 26, 27, 28, 29, 30, 31, 32,
                33, 34, 35, 36, 37, 38, 39, 40,
                41, 42, 43, 44, 45, 46, 47, 48,
                49, 50, 51, 52, 53, 54, 55, 56,
                57, 58, 59, 60, 61, 62, 63, 64,

                // Variable Length String Values
                L1,     // StrL1 (1-byte length)
                L2,     // StrL2 (2-byte length)
                L4,     // StrL4 (4-byte length)
                2,      // StrR1 (Reference string of 1-byte offset)
                3,      // StrR2 (Reference string of 2-byte offset)
                4,      // StrR3 (Reference string of 3-byte offset)
                5,      // StrR4 (Reference string of 4-byte offset)
                9,      // NumUI64

                // Number Values
                2,      // NumUI8
                3,      // NumI16,
                5,      // NumI32,
                9,      // NumI64,
                9,      // NumDbl,
                5,      // Float32
                9,      // Float64
                3,      // Float16

                // Other Value Types
                1,      // Null
                1,      // False
                1,      // True
                17,     // GUID
                0,      // <empty> 0xD4
                0,      // <empty> 0xD5
                0,      // <empty> 0xD6
                2,      // UInt8

                2,      // Int8
                3,      // Int16
                5,      // Int32
                9,      // Int64
                5,      // UInt32
                L1,     // BinL1 (1-byte length)
                L2,     // BinL2 (2-byte length)
                L4,     // BinL4 (4-byte length)

                // Array Type Markers
                1,      // Arr0
                Arr1,   // Arr1
                L1,     // ArrL1 (1-byte length)
                L2,     // ArrL2 (2-byte length)
                L4,     // ArrL4 (4-byte length)
                LC1,    // ArrLC1 (1-byte length and count)
                LC2,    // ArrLC2 (2-byte length and count)
                LC4,    // ArrLC4 (4-byte length and count)

                // Object Type Markers
                1,      // Obj0
                Obj1,   // Obj1
                L1,     // ObjL1 (1-byte length)
                L2,     // ObjL2 (2-byte length)
                L4,     // ObjL4 (4-byte length)
                LC1,    // ObjLC1 (1-byte length and count)
                LC2,    // ObjLC2 (2-byte length and count)
                LC4,    // ObjLC4 (4-byte length and count)

                // Array and Object Special Type Markers
                NC1,    // ArrNumC1 Uniform number array of 1-byte item count
                NC2,    // ArrNumC2 Uniform number array of 2-byte item count
                ANC1,   // Array of 1-byte item count of Uniform number array of 1-byte item count
                ANC2,   // Array of 2-byte item count of Uniform number array of 2-byte item count
                0,      // <empty> 0xF4
                0,      // <empty> 0xF5
                0,      // <empty> 0xF6
                0,      // <empty> 0xF7

                // Special Values
                0,      // <special value reserved> 0xF8
                0,      // <special value reserved> 0xF9
                0,      // <special value reserved> 0xFA
                0,      // <special value reserved> 0xFB
                0,      // <special value reserved> 0xFC
                0,      // <special value reserved> 0xFD
                0,      // <special value reserved> 0xFE
                0,      // Invalid
            }.ToImmutableArray();

            public static long GetValueLength(ReadOnlySpan<byte> buffer)
            {
                long length = ValueLengths.Lookup[buffer[0]];
                if (length < 0)
                {
                    // Length was negative meaning we need to look into the buffer to find the length
                    switch (length)
                    {
                        case L1:
                            length = TypeMarkerLength + OneByteLength + buffer[1];
                            break;
                        case L2:
                            length = TypeMarkerLength + TwoByteLength + MemoryMarshal.Read<ushort>(buffer.Slice(1));
                            break;
                        case L4:
                            length = TypeMarkerLength + FourByteLength + MemoryMarshal.Read<uint>(buffer.Slice(1));
                            break;

                        case LC1:
                            length = TypeMarkerLength + OneByteLength + OneByteCount + buffer[1];
                            break;
                        case LC2:
                            length = TypeMarkerLength + TwoByteLength + TwoByteCount + MemoryMarshal.Read<ushort>(buffer.Slice(1));
                            break;
                        case LC4:
                            length = TypeMarkerLength + FourByteLength + FourByteCount + MemoryMarshal.Read<uint>(buffer.Slice(1));
                            break;

                        case Arr1:
                            long arrayOneItemLength = ValueLengths.GetValueLength(buffer.Slice(1));
                            length = arrayOneItemLength == 0 ? 0 : 1 + arrayOneItemLength;
                            break;

                        case Obj1:
                            long nameLength = ValueLengths.GetValueLength(buffer.Slice(1));
                            if (nameLength == 0)
                            {
                                length = 0;
                            }
                            else
                            {
                                long valueLength = ValueLengths.GetValueLength(buffer.Slice(1 + (int)nameLength));
                                length = TypeMarkerLength + nameLength + valueLength;
                            }
                            break;

                        case CS4L1:
                            length = TypeMarkerLength + OneByteLength + GetCompressedStringLength(buffer[1], numberOfBits: 4);
                            break;
                        case CS7L1:
                            length = TypeMarkerLength + OneByteLength + GetCompressedStringLength(buffer[1], numberOfBits: 7);
                            break;
                        case CS7L2:
                            length = TypeMarkerLength + TwoByteLength + GetCompressedStringLength(GetFixedSizedValue<ushort>(buffer.Slice(start: 1)), numberOfBits: 7);
                            break;

                        case CS4BL1:
                            length = TypeMarkerLength + OneByteLength + OneByteBaseChar + GetCompressedStringLength(buffer[1], numberOfBits: 4);
                            break;
                        case CS5BL1:
                            length = TypeMarkerLength + OneByteLength + OneByteBaseChar + GetCompressedStringLength(buffer[1], numberOfBits: 5);
                            break;
                        case CS6BL1:
                            length = TypeMarkerLength + OneByteLength + OneByteBaseChar + GetCompressedStringLength(buffer[1], numberOfBits: 6);
                            break;

                        case NC1:
                            return 1 + 1 + 1 + (GetUniformNumberArrayItemSize(buffer[1]) * GetFixedSizedValue<byte>(buffer.Slice(2)));
                        case NC2:
                            return 1 + 1 + 2 + (GetUniformNumberArrayItemSize(buffer[1]) * GetFixedSizedValue<ushort>(buffer.Slice(2)));

                        case ANC1:
                            {
                                long nItemSize = GetUniformNumberArrayItemSize(buffer[2]) * GetFixedSizedValue<byte>(buffer.Slice(3));
                                long nItemCount = GetFixedSizedValue<byte>(buffer.Slice(4));

                                return 1 + 3 + 1 + (nItemSize * nItemCount);
                            }

                        case ANC2:
                            {
                                long nItemSize = GetUniformNumberArrayItemSize(buffer[2]) * GetFixedSizedValue<ushort>(buffer.Slice(3));
                                long nItemCount = GetFixedSizedValue<ushort>(buffer.Slice(5));

                                return 1 + 4 + 2 + (nItemSize * nItemCount);
                            }

                        default:
                            throw new ArgumentException($"Invalid variable length type marker length: {length}");
                    }
                }

                return length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int GetCompressedStringLength(int length, int numberOfBits) => ((length * numberOfBits) + 7) / 8;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static int GetUniformNumberArrayItemSize(byte typeMarker)
            {
                return ValueLengths.Lookup[typeMarker] - 1;
            }
        }
    }
}
