// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System.Collections.Immutable;

    internal static partial class JsonBinaryEncoding
    {
        private static class FirstValueOffsets
        {
            /// <summary>
            /// Defines the offset of the first item in an array or object
            /// </summary>
            public static readonly ImmutableArray<int> Offsets = new int[]
            {
                // Encoded literal integer value (32 values)
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,

                // Encoded 1-byte system string (32 values)
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,

                // Encoded 1-byte user string (32 values)
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,

                // Encoded 2-byte user string (8 values)
                0, 0, 0, 0, 0, 0, 0, 0,

                // String Values [0x68, 0x70)
                0, // <empty> 0x68
                0, // <empty> 0x69
                0, // <empty> 0x6A
                0, // <empty> 0x6B
                0, // <empty> 0x6C
                0, // <empty> 0x6D
                0, // <empty> 0x6E
                0, // <empty> 0x6F

                // String Values [0x70, 0x78)
                0, // <empty> 0x70
                0, // <empty> 0x71
                0, // <empty> 0x72
                0, // <empty> 0x73
                0, // <empty> 0x74
                0, // StrGL (Lowercase GUID string)
                0, // StrGU (Uppercase GUID string)
                0, // StrGQ (Double-quoted lowercase GUID string)

                // Compressed strings [0x78, 0x80)
                0, // String 1-byte length - Lowercase hexadecimal digits encoded as 4-bit characters
                0, // String 1-byte length - Uppercase hexadecimal digits encoded as 4-bit characters
                0, // String 1-byte length - Date-time character set encoded as 4-bit characters
                0, // String 1-byte Length - 4-bit packed characters relative to a base value
                0, // String 1-byte Length - 5-bit packed characters relative to a base value
                0, // String 1-byte Length - 6-bit packed characters relative to a base value
                0, // String 1-byte Length - 7-bit packed characters
                0, // String 2-byte Length - 7-bit packed characters

                // TypeMarker-encoded string length (64 values)
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,

                // Variable Length String Values
                0, // StrL1 (1-byte length)
                0, // StrL2 (2-byte length)
                0, // StrL4 (4-byte length)
                0, // StrR1 (Reference string of 1-byte offset)
                0, // StrR2 (Reference string of 2-byte offset)
                0, // StrR3 (Reference string of 3-byte offset)
                0, // StrR4 (Reference string of 4-byte offset)
                0, // <empty> 0xC7

                // Numeric Values
                0, // NumUI8
                0, // NumI16,
                0, // NumI32,
                0, // NumI64,
                0, // NumDbl,
                0, // Float32
                0, // Float64
                0, // <empty> 0xCF

                // Other Value Types
                0, // Null
                0, // False
                0, // True
                0, // GUID
                0, // <empty> 0xD4
                0, // <empty> 0xD5
                0, // <empty> 0xD6
                0, // <empty> 0xD7

                0, // Int8
                0, // Int16
                0, // Int32
                0, // Int64
                0, // UInt32
                0, // BinL1 (1-byte length)
                0, // BinL2 (2-byte length)
                0, // BinL4 (4-byte length)

                // Array Type Markers
                1, // Arr0
                1, // Arr1
                2, // ArrL1 (1-byte length)
                3, // ArrL2 (2-byte length)
                5, // ArrL4 (4-byte length)
                3, // ArrLC1 (1-byte length and count)
                5, // ArrLC2 (2-byte length and count)
                9, // ArrLC4 (4-byte length and count)

                // Object Type Markers
                1, // Obj0
                1, // Obj1
                2, // ObjL1 (1-byte length)
                3, // ObjL2 (2-byte length)
                5, // ObjL4 (4-byte length)
                3, // ObjLC1 (1-byte length and count)
                5, // ObjLC2 (2-byte length and count)
                9, // ObjLC4 (4-byte length and count)

                // Empty Range
                0, // <empty> 0xF0
                0, // <empty> 0xF1
                0, // <empty> 0xF2
                0, // <empty> 0xF3
                0, // <empty> 0xF4
                0, // <empty> 0xF5
                0, // <empty> 0xF7
                0, // <empty> 0xF8

                // Special Values
                0, // <special value reserved> 0xF8
                0, // <special value reserved> 0xF9
                0, // <special value reserved> 0xFA
                0, // <special value reserved> 0xFB
                0, // <special value reserved> 0xFC
                0, // <special value reserved> 0xFD
                0, // <special value reserved> 0xFE
                0, // Invalid 0xFF
            }.ToImmutableArray();
        }
    }
}
