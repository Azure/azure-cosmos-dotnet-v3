// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System.Collections.Generic;
    using System.Collections.Immutable;

    internal static partial class JsonBinaryEncoding
    {
        private static class StringLengths
        {
            private const int UsrStr1 = -1;
            private const int UsrStr2 = -2;
            private const int StrL1 = -3;
            private const int StrL2 = -4;
            private const int StrL4 = -5;
            private const int StrR1 = -6;
            private const int StrR2 = -7;
            private const int StrR3 = -8;
            private const int StrR4 = -9;
            private const int StrComp = -10;
            private const int NotStr = -11;

            /// <summary>
            /// Lookup table for encoded string length for each TypeMarker value (0 to 255)
            /// The lengths are encoded as follows:
            /// - Non-Negative Value: The TypeMarker encodes the string length
            /// - Negative Value: System or user dictionary encoded string, or encoded string length that follows the TypeMarker
            /// </summary>
            public static readonly ImmutableArray<int> Lengths = new int[]
            {
                // Encoded literal integer value (32 values)
                NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr,
                NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr,
                NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr,
                NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr, NotStr,

                // Encoded 1-byte system string (32 values)
                SystemStrings.Strings[0].Utf8String.Length, SystemStrings.Strings[1].Utf8String.Length,
                SystemStrings.Strings[2].Utf8String.Length, SystemStrings.Strings[3].Utf8String.Length,
                SystemStrings.Strings[4].Utf8String.Length, SystemStrings.Strings[5].Utf8String.Length,
                SystemStrings.Strings[6].Utf8String.Length, SystemStrings.Strings[7].Utf8String.Length,
                SystemStrings.Strings[8].Utf8String.Length, SystemStrings.Strings[9].Utf8String.Length,
                SystemStrings.Strings[10].Utf8String.Length, SystemStrings.Strings[11].Utf8String.Length,
                SystemStrings.Strings[12].Utf8String.Length, SystemStrings.Strings[13].Utf8String.Length,
                SystemStrings.Strings[14].Utf8String.Length, SystemStrings.Strings[15].Utf8String.Length,
                SystemStrings.Strings[16].Utf8String.Length, SystemStrings.Strings[17].Utf8String.Length,
                SystemStrings.Strings[18].Utf8String.Length, SystemStrings.Strings[19].Utf8String.Length,
                SystemStrings.Strings[20].Utf8String.Length, SystemStrings.Strings[21].Utf8String.Length,
                SystemStrings.Strings[22].Utf8String.Length, SystemStrings.Strings[23].Utf8String.Length,
                SystemStrings.Strings[24].Utf8String.Length, SystemStrings.Strings[25].Utf8String.Length,
                SystemStrings.Strings[26].Utf8String.Length, SystemStrings.Strings[27].Utf8String.Length,
                SystemStrings.Strings[28].Utf8String.Length, SystemStrings.Strings[29].Utf8String.Length,
                SystemStrings.Strings[30].Utf8String.Length, SystemStrings.Strings[31].Utf8String.Length,

                // Encoded 1-byte user string (32 values)
                UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1,
                UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1,
                UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1,
                UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1, UsrStr1,
    
                // Encoded 2-byte user string (8 values) 
                UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2, UsrStr2,

                // String Values [0x68, 0x70)
                NotStr,     // <empty> 0x68
                NotStr,     // <empty> 0x69
                NotStr,     // <empty> 0x6A
                NotStr,     // <empty> 0x6B
                NotStr,     // <empty> 0x6C
                NotStr,     // <empty> 0x6D
                NotStr,     // <empty> 0x6E
                NotStr,     // <empty> 0x6F

                // Empty Range
                NotStr,     // <empty> 0x70
                NotStr,     // <empty> 0x71
                NotStr,     // <empty> 0x72
                NotStr,     // <empty> 0x73
                NotStr,     // <empty> 0x74
                36,         // StrGL (Lowercase GUID string)
                36,         // StrGU (Uppercase GUID string)
                38,         // StrGQ (Double-quoted lowercase GUID string)

                // Compressed strings [0x78, 0x80)
                StrComp,    // String 1-byte length - Lowercase hexadecimal digits encoded as 4-bit characters
                StrComp,    // String 1-byte length - Uppercase hexadecimal digits encoded as 4-bit characters
                StrComp,    // String 1-byte length - Date-time character set encoded as 4-bit characters
                StrComp,    // String 1-byte Length - 4-bit packed characters relative to a base value
                StrComp,    // String 1-byte Length - 5-bit packed characters relative to a base value
                StrComp,    // String 1-byte Length - 6-bit packed characters relative to a base value
                StrComp,    // String 1-byte Length - 7-bit packed characters
                StrComp,    // String 2-byte Length - 7-bit packed characters

                // TypeMarker-encoded string length (64 values)
                0, 1, 2, 3, 4, 5, 6, 7,
                8, 9, 10, 11, 12, 13, 14, 15,
                16, 17, 18, 19, 20, 21, 22, 23,
                24, 25, 26, 27, 28, 29, 30, 31,
                32, 33, 34, 35, 36, 37, 38, 39,
                40, 41, 42, 43, 44, 45, 46, 47,
                48, 49, 50, 51, 52, 53, 54, 55,
                56, 57, 58, 59, 60, 61, 62, 63,

                // Variable Length String Values
                StrL1,      // StrL1 (1-byte length)
                StrL2,      // StrL2 (2-byte length)
                StrL4,      // StrL4 (4-byte length)
                StrR1,      // StrR1 (Reference string of 1-byte offset)
                StrR2,      // StrR2 (Reference string of 2-byte offset)
                StrR3,      // StrR3 (Reference string of 3-byte offset)
                StrR4,      // StrR4 (Reference string of 4-byte offset)
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
                NotStr,     // GUID
                NotStr,     // <empty> 0xD4
                NotStr,     // <empty> 0xD5
                NotStr,     // <empty> 0xD6
                NotStr,     // <empty> 0xD7

                NotStr,     // Int8
                NotStr,     // Int16
                NotStr,     // Int32
                NotStr,     // Int64
                NotStr,     // UInt32
                NotStr,     // BinL1 (1-byte length)
                NotStr,     // BinL2 (2-byte length)
                NotStr,     // BinL4 (4-byte length)

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
            }.ToImmutableArray();
        }
    }
}
