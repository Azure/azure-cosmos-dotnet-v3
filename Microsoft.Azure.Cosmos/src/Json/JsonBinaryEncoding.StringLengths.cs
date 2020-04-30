// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System.Collections.Generic;

    internal static partial class JsonBinaryEncoding
    {
        private static class StringLengths
        {
            private const int SysStr1 = -1;
            private const int UsrStr1 = -2;
            private const int UsrStr2 = -3;
            private const int StrL1 = -4;
            private const int StrL2 = -5;
            private const int StrL4 = -6;
            private const int NotStr = -7;

            /// <summary>
            /// Lookup table for encoded string length for each TypeMarker value (0 to 255)
            /// The lengths are encoded as follows:
            /// - Non-Negative Value: The TypeMarker encodes the string length
            /// - Negative Value: System or user dictionary encoded string, or encoded string length that follows the TypeMarker
            /// </summary>
            private static readonly int[] lengths =
            {
                // Encoded literal integer value (32 values)
                NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr,
                NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr,
                NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr,
                NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr,

                // Encoded 1-byte system string (32 values)
                SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1,
                SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1,
                SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1,
                SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1, SysStr1,

                // Encoded 1-byte user string (32 values)
                UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1,
                UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1,
                UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1,
                UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1,
    
                // Encoded 2-byte user string (24 values) 
                UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2,
                UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2,
                UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2,
                UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2,

                // TypeMarker-encoded string length (64 values)
                0, 1, 2, 3, 4, 5, 6, 7,
                8, 9, 10, 11, 12, 13, 14, 15,
                16, 17, 18, 19, 20, 21, 22, 23,
                24, 25, 26, 27, 28, 29, 30, 31,
                32, 33, 34, 35, 36, 37, 38, 39,
                40, 41, 42, 43, 44, 45, 46, 47,
                48, 49, 50, 51, 52, 53, 54, 55,
                56, 57, 58, 59, 60, 61, 62, 63,

                // Variable Length String Values / Binary Values
                StrL1,      // StrL1 (1-byte length)
                StrL2,      // StrL2 (2-byte length)
                StrL4,      // StrL4 (4-byte length)
                NotStr,     // BinL1 (1-byte length)
                NotStr,     // BinL2 (2-byte length)
                NotStr,     // BinL4 (4-byte length)
                NotStr,     // <empty> 0xC6
                NotStr,     // <empty> 0xC7

                // Number Values
                NotStr,     // NumUI8
                NotStr,     // NumI16,
                NotStr,     // NumI32,
                NotStr,     // NumI64,
                NotStr,     // NumDbl,
                NotStr,     // Float32
                NotStr,     // Float64
                NotStr,     // <empty> 0xCF

                // Other Value Types
                NotStr,     // Null
                NotStr,     // False
                NotStr,     // True
                NotStr,     // Guid
                NotStr,     // <empty> 0xD4
                NotStr,     // <empty> 0xD5
                NotStr,     // <empty> 0xD6
                NotStr,     // <empty> 0xD7

                NotStr,     // Int8
                NotStr,     // Int16
                NotStr,     // Int32
                NotStr,     // Int64
                NotStr,     // UInt32
                NotStr,     // <empty> 0xDD
                NotStr,     // <empty> 0xDE
                NotStr,     // <empty> 0xDF

                // Array Type Markers
                NotStr,     // Arr0
                NotStr,     // Arr1
                NotStr,     // ArrL1 (1-byte length)
                NotStr,     // ArrL2 (2-byte length)
                NotStr,     // ArrL4 (4-byte length)
                NotStr,     // ArrLC1 (1-byte length and count)
                NotStr,     // ArrLC2 (2-byte length and count)
                NotStr,     // ArrLC4 (4-byte length and count)

                // Object Type Markers
                NotStr,     // Obj0
                NotStr,     // Obj1
                NotStr,     // ObjL1 (1-byte length)
                NotStr,     // ObjL2 (2-byte length)
                NotStr,     // ObjL4 (4-byte length)
                NotStr,     // ObjLC1 (1-byte length and count)
                NotStr,     // ObjLC2 (2-byte length and count)
                NotStr,     // ObjLC4 (4-byte length and count)

                // Empty Range
                NotStr,     // <empty> 0xF0
                NotStr,     // <empty> 0xF1
                NotStr,     // <empty> 0xF2
                NotStr,     // <empty> 0xF3
                NotStr,     // <empty> 0xF4
                NotStr,     // <empty> 0xF5
                NotStr,     // <empty> 0xF7
                NotStr,     // <empty> 0xF8

                // Special Values
                NotStr,     // <special value reserved> 0xF8
                NotStr,     // <special value reserved> 0xF9
                NotStr,     // <special value reserved> 0xFA
                NotStr,     // <special value reserved> 0xFB
                NotStr,     // <special value reserved> 0xFC
                NotStr,     // <special value reserved> 0xFD
                NotStr,     // <special value reserved> 0xFE
                NotStr,     // Invalid
            };

            public static IReadOnlyList<int> Lengths
            {
                get
                {
                    return StringLengths.lengths;
                }
            }
        }
    }
}
