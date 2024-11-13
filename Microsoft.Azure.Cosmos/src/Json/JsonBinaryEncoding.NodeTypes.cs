// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System.Collections.Immutable;

    internal static partial class JsonBinaryEncoding
    {
        public static class NodeTypes
        {
            private const JsonNodeType Array = JsonNodeType.Array;
            private const JsonNodeType Binary = JsonNodeType.Binary;
            private const JsonNodeType False = JsonNodeType.False;
            private const JsonNodeType Float32 = JsonNodeType.Float32;
            private const JsonNodeType Float64 = JsonNodeType.Float64;
            private const JsonNodeType Guid = JsonNodeType.Guid;
            private const JsonNodeType Int16 = JsonNodeType.Int16;
            private const JsonNodeType Int32 = JsonNodeType.Int32;
            private const JsonNodeType Int64 = JsonNodeType.Int64;
            private const JsonNodeType Int8 = JsonNodeType.Int8;
            private const JsonNodeType Null = JsonNodeType.Null;
            private const JsonNodeType Number = JsonNodeType.Number64;
            private const JsonNodeType Object = JsonNodeType.Object;
            private const JsonNodeType String = JsonNodeType.String;
            private const JsonNodeType True = JsonNodeType.True;
            private const JsonNodeType UInt32 = JsonNodeType.UInt32;
            private const JsonNodeType Unknown = JsonNodeType.Unknown;

            public static readonly ImmutableArray<JsonNodeType> Lookup = new JsonNodeType[]
            {
                // Encoded literal integer value (32 values)
                Number, Number, Number, Number, Number, Number, Number, Number,
                Number, Number, Number, Number, Number, Number, Number, Number,
                Number, Number, Number, Number, Number, Number, Number, Number,
                Number, Number, Number, Number, Number, Number, Number, Number,

                // Encoded 1-byte system string (32 values)
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,

                // Encoded 1-byte user string (32 values)
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,

                // Encoded 2-byte user string (8 values)
                String, String, String, String, String, String, String, String,

                // String Values [0x68, 0x70)
                Unknown,    // <empty> 0x68
                Unknown,    // <empty> 0x69
                Unknown,    // <empty> 0x6A
                Unknown,    // <empty> 0x6B
                Unknown,    // <empty> 0x6C
                Unknown,    // <empty> 0x6D
                Unknown,    // <empty> 0x6E
                Unknown,    // <empty> 0x6F

                // String Values [0x70, 0x78)
                Unknown,    // <empty> 0x70
                Unknown,    // <empty> 0x71
                Unknown,    // <empty> 0x72
                Unknown,    // <empty> 0x73
                Unknown,    // <empty> 0x74
                String,     // StrGL (Lowercase GUID string)
                String,     // StrGU (Uppercase GUID string)
                String,     // StrGQ (Double-quoted lowercase GUID string)

                // Compressed strings [0x78, 0x80)
                String,     // String 1-byte length - Lowercase hexadecimal digits encoded as 4-bit characters
                String,     // String 1-byte length - Uppercase hexadecimal digits encoded as 4-bit characters
                String,     // String 1-byte length - Date-time character set encoded as 4-bit characters
                String,     // String 1-byte Length - 4-bit packed characters relative to a base value
                String,     // String 1-byte Length - 5-bit packed characters relative to a base value
                String,     // String 1-byte Length - 6-bit packed characters relative to a base value
                String,     // String 1-byte Length - 7-bit packed characters
                String,     // String 2-byte Length - 7-bit packed characters

                // TypeMarker-encoded string length (64 values)
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,
                String, String, String, String, String, String, String, String,

                // Variable Length String Values
                String,     // StrL1 (1-byte length)
                String,     // StrL2 (2-byte length)
                String,     // StrL4 (4-byte length)
                String,     // StrR1 (Reference string of 1-byte offset)
                String,     // StrR2 (Reference string of 2-byte offset)
                String,     // StrR3 (Reference string of 3-byte offset)
                String,     // StrR4 (Reference string of 4-byte offset)
                Number,     // NumUI64

                // Number Values
                Number,     // NumUI8
                Number,     // NumI16,
                Number,     // NumI32,
                Number,     // NumI64,
                Number,     // NumDbl,
                Float32,    // Float32
                Float64,    // Float64
                Unknown,    // Float16 (No corresponding JsonNodeType at the moment)

                // Other Value Types
                Null,       // Null
                False,      // False
                True,       // True
                Guid,       // GUID
                Unknown,    // <empty> 0xD4
                Unknown,    // <empty> 0xD5
                Unknown,    // <empty> 0xD6
                Unknown,    // UInt8 (No corresponding JsonNodeType at the moment)

                Int8,       // Int8
                Int16,      // Int16
                Int32,      // Int32
                Int64,      // Int64
                UInt32,     // UInt32
                Binary,     // BinL1 (1-byte length)
                Binary,     // BinL2 (2-byte length)
                Binary,     // BinL4 (4-byte length)

                // Array Type Markers
                Array,      // Arr0
                Array,      // Arr1 <unknown>
                Array,      // ArrL1 (1-byte length)
                Array,      // ArrL2 (2-byte length)
                Array,      // ArrL4 (4-byte length)
                Array,      // ArrLC1 (1-byte length and count)
                Array,      // ArrLC2 (2-byte length and count)
                Array,      // ArrLC4 (4-byte length and count)

                // Object Type Markers
                Object,     // Obj0
                Object,     // Obj1 <unknown>
                Object,     // ObjL1 (1-byte length)
                Object,     // ObjL2 (2-byte length)
                Object,     // ObjL4 (4-byte length)
                Object,     // ObjLC1 (1-byte length and count)
                Object,     // ObjLC2 (2-byte length and count)
                Object,     // ObjLC4 (4-byte length and count)

                // Array and Object Special Type Markers
                Array,      // ArrNumC1 Uniform number array of 1-byte item count
                Array,      // ArrNumC2 Uniform number array of 2-byte item count
                Array,      // Array of 1-byte item count of Uniform number array of 1-byte item count
                Array,      // Array of 2-byte item count of Uniform number array of 2-byte item count
                Unknown,    // <empty> 0xF4
                Unknown,    // <empty> 0xF5
                Unknown,    // <empty> 0xF6
                Unknown,    // <empty> 0xF7

                // Special Values
                Unknown,    // <special value reserved> 0xF8
                Unknown,    // <special value reserved> 0xF9
                Unknown,    // <special value reserved> 0xFA
                Unknown,    // <special value reserved> 0xFB
                Unknown,    // <special value reserved> 0xFC
                Unknown,    // <special value reserved> 0xFD
                Unknown,    // <special value reserved> 0xFE
                Unknown,    // Invalid
            }.ToImmutableArray();
        }
    }
}
