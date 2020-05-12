// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Runtime.InteropServices;

    internal static partial class JsonBinaryEncoding
    {
        private static class ValueLengths
        {
            private const int L1 = -1;
            private const int L2 = -2;
            private const int L4 = -3;
            private const int LC1 = -4;
            private const int LC2 = -5;
            private const int LC4 = -6;
            private const int Arr1 = -7;
            private const int Obj1 = -8;

            /// <summary>
            /// Lookup table for encoded value length for each TypeMarker value (0 to 255)
            /// The lengths are encoded as follows:
            /// - Positive Value: The length of the value including its TypeMarker
            /// - Negative Value: The length is encoded as an integer of size equals to abs(value) following the TypeMarker byte
            /// - Zero Value: The length is unknown (for instance an unassigned type marker)
            /// </summary>
            private static readonly int[] lengths =
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
    
                // Encoded 2-byte user string (32 values)
                2, 2, 2, 2, 2, 2, 2, 2,
                2, 2, 2, 2, 2, 2, 2, 2,
                2, 2, 2, 2, 2, 2, 2, 2,
                2, 2, 2, 2, 2, 2, 2, 2,

                // TypeMarker-encoded string length (64 values)
                1, 2, 3, 4, 5, 6, 7, 8,
                9, 10, 11, 12, 13, 14, 15, 16,
                17, 18, 19, 20, 21, 22, 23, 24,
                25, 26, 27, 28, 29, 30, 31, 32,
                33, 34, 35, 36, 37, 38, 39, 40,
                41, 42, 43, 44, 45, 46, 47, 48,
                49, 50, 51, 52, 53, 54, 55, 56,
                57, 58, 59, 60, 61, 62, 63, 64,

                // Variable Length String Values / Binary Values
                L1,     // StrL1 (1-byte length)
                L2,     // StrL2 (2-byte length)
                L4,     // StrL4 (4-byte length)
                L1,     // BinL1 (1-byte length)
                L2,     // BinL2 (2-byte length)
                L4,     // BinL4 (4-byte length)
                0,      // <empty> 0xC6
                0,      // <empty> 0xC7

                // Number Values
                2,      // NumUI8
                3,      // NumI16,
                5,      // NumI32,
                9,      // NumI64,
                9,      // NumDbl,
                5,      // Float32
                9,      // Float64
                0,      // <empty> 0xCF

                // Other Value Types
                1,      // Null
                1,      // False
                1,      // True
                17,     // Guid
                0,      // <empty> 0xD4
                0,      // <empty> 0xD5
                0,      // <empty> 0xD6
                0,      // <empty> 0xD7

                2,      // Int8
                3,      // Int16
                5,      // Int32
                9,      // Int64
                5,      // UInt32
                0,      // <empty> 0xDD
                0,      // <empty> 0xDE
                0,      // <empty> 0xDF

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

                // Empty Range
                0,      // <empty> 0xF0
                0,      // <empty> 0xF1
                0,      // <empty> 0xF2
                0,      // <empty> 0xF3
                0,      // <empty> 0xF4
                0,      // <empty> 0xF5
                0,      // <empty> 0xF7
                0,      // <empty> 0xF8

                // Special Values
                0,      // <special value reserved> 0xF8
                0,      // <special value reserved> 0xF9
                0,      // <special value reserved> 0xFA
                0,      // <special value reserved> 0xFB
                0,      // <special value reserved> 0xFC
                0,      // <special value reserved> 0xFD
                0,      // <special value reserved> 0xFE
                0,      // Invalid
            };

            public static long GetValueLength(ReadOnlySpan<byte> buffer)
            {
                long length = ValueLengths.lengths[buffer[0]];
                if (length < 0)
                {
                    // Length was negative meaning we need to look into the buffer to find the length
                    switch (length)
                    {
                        case L1:
                            length = TypeMarkerLength + OneByteLength + MemoryMarshal.Read<byte>(buffer.Slice(1));
                            break;
                        case L2:
                            length = TypeMarkerLength + TwoByteLength + MemoryMarshal.Read<ushort>(buffer.Slice(1));
                            break;
                        case L4:
                            length = TypeMarkerLength + FourByteLength + MemoryMarshal.Read<uint>(buffer.Slice(1));
                            break;
                        case LC1:
                            length = TypeMarkerLength + OneByteLength + OneByteCount + MemoryMarshal.Read<byte>(buffer.Slice(1));
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
                        default:
                            throw new ArgumentException($"Invalid variable length type marker length: {length}");
                    }
                }

                return length;
            }
        }
    }
}
