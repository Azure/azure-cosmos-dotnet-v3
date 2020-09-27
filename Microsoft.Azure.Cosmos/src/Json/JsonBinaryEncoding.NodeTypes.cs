// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
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

            private static readonly JsonNodeType[] Types = new JsonNodeType[]
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

                    // Encoded 2-byte user string (32 values)
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,

                    // TypeMarker-encoded string length (64 values)
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,
                    String, String, String, String, String, String, String, String,

                    // Variable Length String Values / Binary Values
                    String,     // StrL1 (1-byte length)
                    String,     // StrL2 (2-byte length)
                    String,     // StrL4 (4-byte length)
                    Binary,     // BinL1 (1-byte length)
                    Binary,     // BinL2 (2-byte length)
                    Binary,     // BinL4 (4-byte length)
                    Unknown,    // <empty> 0xC6
                    Unknown,    // <empty> 0xC7

                    // Number Values
                    Number,     // NumUI8
                    Number,     // NumI16,
                    Number,     // NumI32,
                    Number,     // NumI64,
                    Number,     // NumDbl,
                    Float32,    // Float32
                    Float64,    // Float64
                    Unknown,    // <empty> 0xCF

                    // Other Value Types
                    Null,       // Null
                    False,      // False
                    True,       // True
                    Guid,       // Guid
                    Unknown,    // <empty> 0xD4
                    Unknown,    // <empty> 0xD5
                    Unknown,    // <empty> 0xD6
                    Unknown,    // <empty> 0xD7

                    Int8,       // Int8
                    Int16,      // Int16
                    Int32,      // Int32
                    Int64,      // Int64
                    UInt32,     // UInt32
                    Unknown,    // <empty> 0xDD
                    Unknown,    // <empty> 0xDE
                    Unknown,    // <empty> 0xDF

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

                    // Empty Range
                    Unknown,    // <empty> 0xF0
                    Unknown,    // <empty> 0xF1
                    Unknown,    // <empty> 0xF2
                    Unknown,    // <empty> 0xF3
                    Unknown,    // <empty> 0xF4
                    Unknown,    // <empty> 0xF5
                    Unknown,    // <empty> 0xF7
                    Unknown,    // <empty> 0xF8

                    // Special Values
                    Unknown,    // <special value reserved> 0xF8
                    Unknown,    // <special value reserved> 0xF9
                    Unknown,    // <special value reserved> 0xFA
                    Unknown,    // <special value reserved> 0xFB
                    Unknown,    // <special value reserved> 0xFC
                    Unknown,    // <special value reserved> 0xFD
                    Unknown,    // <special value reserved> 0xFE
                    Unknown,    // Invalid
            };

            public static JsonNodeType GetNodeType(byte typeMarker)
            {
                return NodeTypes.Types[typeMarker];
            }
        }
    }
}
