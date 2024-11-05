//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System.Runtime.CompilerServices;

    internal static partial class JsonBinaryEncoding
    {
        /// <summary>
        /// Defines the set of type-marker values that are used to encode JSON value
        /// </summary>
        public readonly struct TypeMarker
        {
            #region [0x00, 0x20): Encoded literal integer value (32 values)
            /// <summary>
            /// The first integer what can be encoded in the type marker itself.
            /// </summary>
            /// <example>1 can be encoded as LiterIntMin + 1.</example>
            public const byte LiteralIntMin = 0x00;

            /// <summary>
            /// The last integer what can be encoded in the type marker itself.
            /// </summary>
            /// <example>1 can be encoded as LiterIntMin + 1.</example>
            public const byte LiteralIntMax = LiteralIntMin + 32;
            #endregion

            #region [0x20, 0x40): Encoded 1-byte system string (32 values)
            /// <summary>
            /// The first type marker for a system string whose value can be encoded in a 1 byte type marker.
            /// </summary>
            public const byte SystemString1ByteLengthMin = LiteralIntMax;

            /// <summary>
            /// The last type marker for a system string whose value can be encoded in a 1 byte type marker.
            /// </summary>
            public const byte SystemString1ByteLengthMax = SystemString1ByteLengthMin + 32;
            #endregion

            #region [0x40, 0x60): Encoded 1-byte user string (32 values)
            /// <summary>
            /// The first type marker for a user string whose value can be encoded in a 1 byte type marker.
            /// </summary>
            public const byte UserString1ByteLengthMin = SystemString1ByteLengthMax;

            /// <summary>
            /// The last type marker for a user string whose value can be encoded in a 1 byte type marker.
            /// </summary>
            public const byte UserString1ByteLengthMax = UserString1ByteLengthMin + 32;
            #endregion

            #region [0x60, 0x68): 2-byte user string (8 values)
            /// <summary>
            /// The first type marker for a system string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte UserString2ByteLengthMin = UserString1ByteLengthMax;

            /// <summary>
            /// The last type marker for a system string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte UserString2ByteLengthMax = UserString2ByteLengthMin + 8;
            #endregion

            #region [0x68, 0x70): String Values (8 Values)
            // <empty> 0x68
            // <empty> 0x69
            // <empty> 0x6A
            // <empty> 0x6B
            // <empty> 0x6C
            // <empty> 0x6D
            // <empty> 0x6E
            // <empty> 0x6F
            #endregion

            #region [0x70, 0x78): String Values (8 Values)
            // <empty> 0x70
            // <empty> 0x71
            // <empty> 0x72
            // <empty> 0x73
            // <empty> 0x74

            /// <summary>
            /// The type marker for a guid string with only lowercase characters.
            /// </summary>
            public const byte LowercaseGuidString = 0x75;

            /// <summary>
            /// The type marker for a guid string with only uppercase characaters.
            /// </summary>
            public const byte UppercaseGuidString = 0x76;

            /// <summary>
            /// The type marker for a guid string that is double quoted (ETAG).
            /// </summary>
            public const byte DoubleQuotedLowercaseGuidString = 0x77;
            #endregion

            #region [0x78, 0x80): Compressed String (8 Values)
            /// <summary>
            /// String 1-byte length - Lowercase hexadecimal digits encoded as 4-bit characters
            /// </summary>
            public const byte CompressedLowercaseHexString = 0x78;

            /// <summary>
            /// String 1-byte length - Uppercase hexadecimal digits encoded as 4-bit characters
            /// </summary>
            public const byte CompressedUppercaseHexString = 0x79;

            /// <summary>
            /// String 1-byte length - Date-time character set encoded as 4-bit characters
            /// </summary>
            public const byte CompressedDateTimeString = 0x7A;

            /// <summary>
            /// String 1-byte Length - 4-bit packed characters relative to a base value
            /// </summary>
            public const byte Packed4BitString = 0x7B;

            /// <summary>
            /// String 1-byte Length - 5-bit packed characters relative to a base value
            /// </summary>
            public const byte Packed5BitString = 0x7C;

            /// <summary>
            /// String 1-byte Length - 6-bit packed characters relative to a base value
            /// </summary>
            public const byte Packed6BitString = 0x7D;

            /// <summary>
            /// String 1-byte Length - 7-bit packed characters
            /// </summary>
            public const byte Packed7BitStringLength1 = 0x7E;

            /// <summary>
            /// String 2-byte Length - 7-bit packed characters
            /// </summary>
            public const byte Packed7BitStringLength2 = 0x7F;
            #endregion

            #region [0x80, 0xC0): Encoded string length (64 values)
            /// <summary>
            /// The first type marker for a string whose length is encoded.
            /// </summary>
            /// <example>EncodedStringLengthMin + 1 is a type marker for a string with length 1.</example>
            public const byte EncodedStringLengthMin = 0x80;

            /// <summary>
            /// The last type marker for a string whose length is encoded.
            /// </summary>
            /// <example>EncodedStringLengthMin + 1 is a type marker for a string with length 1.</example>
            public const byte EncodedStringLengthMax = EncodedStringLengthMin + 64;
            #endregion

            #region [0xC0, 0xC8): Variable Length Strings
            /// <summary>
            /// Type marker for a String of 1-byte length
            /// </summary>
            public const byte String1ByteLength = 0xC0;

            /// <summary>
            /// Type marker for a String of 2-byte length
            /// </summary>
            public const byte String2ByteLength = 0xC1;

            /// <summary>
            /// Type marker for a String of 4-byte length
            /// </summary>
            public const byte String4ByteLength = 0xC2;

            /// <summary>
            /// Reference string of 1-byte offset
            /// </summary>
            public const byte ReferenceString1ByteOffset = 0xC3;

            /// <summary>
            /// Reference string of 2-byte offset
            /// </summary>
            public const byte ReferenceString2ByteOffset = 0xC4;

            /// <summary>
            /// Reference string of 3-byte offset
            /// </summary>
            public const byte ReferenceString3ByteOffset = 0xC5;

            /// <summary>
            /// Reference string of 4-byte offset
            /// </summary>
            public const byte ReferenceString4ByteOffset = 0xC6;

            /// <summary>
            /// Type marker for a 8-byte unsigned integer
            /// </summary>
            public const byte NumberUInt64 = 0xC7;
            #endregion

            #region [0xC8, 0xD0): Number Values
            /// <summary>
            /// Type marker for a 1-byte unsigned integer
            /// </summary>
            public const byte NumberUInt8 = 0xC8;

            /// <summary>
            /// Type marker for a 2-byte singed integer
            /// </summary>
            public const byte NumberInt16 = 0xC9;

            /// <summary>
            /// Type marker for a 4-byte singed integer
            /// </summary>
            public const byte NumberInt32 = 0xCA;

            /// <summary>
            /// Type marker for a 8-byte singed integer
            /// </summary>
            public const byte NumberInt64 = 0xCB;

            /// <summary>
            /// Type marker for a Double-precession floating point number
            /// </summary>
            public const byte NumberDouble = 0xCC;

            /// <summary>
            /// Type marker for a single precision floating point number.
            /// </summary>
            public const byte Float32 = 0xCD;

            /// <summary>
            /// Type marker for double precision floating point number.
            /// </summary>
            public const byte Float64 = 0xCE;

            /// <summary>
            /// Type marker for 16-bit floating point number.
            /// </summary>
            public const byte Float16 = 0xCF;
            #endregion

            #region [0xDO, 0xE0): Other Value Types
            /// <summary>
            /// The type marker for a JSON null value.
            /// </summary>
            public const byte Null = 0xD0;

            /// <summary>
            /// The type marker for a JSON false value.
            /// </summary>
            public const byte False = 0xD1;

            /// <summary>
            /// The type marker for a JSON true value
            /// </summary>
            public const byte True = 0xD2;

            /// <summary>
            /// The type marker for a GUID
            /// </summary>
            public const byte Guid = 0xD3;

            // <other types empty> 0xD4
            // <other types empty> 0xD5
            // <other types empty> 0xD6

            /// <summary>
            /// The type marker for a 1-byte unsigned integer value.
            /// </summary>
            public const byte UInt8 = 0xD7;

            /// <summary>
            /// The type marker for a 1-byte signed integer value.
            /// </summary>
            public const byte Int8 = 0xD8;

            /// <summary>
            /// The type marker for a 2-byte signed integer value.
            /// </summary>
            public const byte Int16 = 0xD9;

            /// <summary>
            /// The type marker for a 4-byte signed integer value.
            /// </summary>
            public const byte Int32 = 0xDA;

            /// <summary>
            /// The type marker for a 8-byte signed integer value.
            /// </summary>
            public const byte Int64 = 0xDB;

            /// <summary>
            /// The type marker for a 4-byte signed integer value.
            /// </summary>
            public const byte UInt32 = 0xDC;

            /// <summary>
            /// Type marker for binary payloads with 1 byte length.
            /// </summary>
            public const byte Binary1ByteLength = 0xDD;

            /// <summary>
            /// Type marker for binary payloads with 2 byte length.
            /// </summary>
            public const byte Binary2ByteLength = 0xDE;

            /// <summary>
            /// Type marker for binary payloads with 4 byte length.
            /// </summary>
            public const byte Binary4ByteLength = 0xDF;
            #endregion

            #region [0xE0, 0xE8): Array Type Markers
            /// <summary>
            /// Empty array type marker.
            /// </summary>
            public const byte Arr0 = 0xE0;

            /// <summary>
            /// Single-item array type marker.
            /// </summary>
            public const byte Arr1 = 0xE1;

            /// <summary>
            /// Array of 1-byte length type marker.
            /// </summary>
            public const byte ArrL1 = 0xE2;

            /// <summary>
            /// Array of 2-byte length type marker.
            /// </summary>
            public const byte ArrL2 = 0xE3;

            /// <summary>
            /// Array of 4-byte length type marker.
            /// </summary>
            public const byte ArrL4 = 0xE4;

            /// <summary>
            /// Array of 1-byte length and item count type marker.
            /// </summary>
            public const byte ArrLC1 = 0xE5;

            /// <summary>
            /// Array of 2-byte length and item count type marker.
            /// </summary>
            public const byte ArrLC2 = 0xE6;

            /// <summary>
            /// Array of 4-byte length and item count type marker.
            /// </summary>
            public const byte ArrLC4 = 0xE7;
            #endregion

            #region [0xE8, 0xF0): Object Type Markers
            /// <summary>
            /// Empty object type marker.
            /// </summary>
            public const byte Obj0 = 0xE8;

            /// <summary>
            /// Single-property object type marker.
            /// </summary>
            public const byte Obj1 = 0xE9;

            /// <summary>
            /// Object of 1-byte length type marker.
            /// </summary>
            public const byte ObjL1 = 0xEA;

            /// <summary>
            /// Object of 2-byte length type marker.
            /// </summary>
            public const byte ObjL2 = 0xEB;

            /// <summary>
            /// Object of 4-byte length type maker.
            /// </summary>
            public const byte ObjL4 = 0xEC;

            /// <summary>
            /// Object of 1-byte length and property count type marker.
            /// </summary>
            public const byte ObjLC1 = 0xED;

            /// <summary>
            /// Object of 2-byte length and property count type marker.
            /// </summary>
            public const byte ObjLC2 = 0xEE;

            /// <summary>
            /// Object of 4-byte length and property count type marker.
            /// </summary>
            public const byte ObjLC4 = 0xEF;
            #endregion

            #region [0xF0, 0xF8): Special Arrays Type Markers
            /// <summary>
            /// Uniform number array of 1-byte item count.
            /// </summary>
            public const byte ArrNumC1 = 0xF0;

            /// <summary>
            /// Uniform number array of 2-byte item count.
            /// </summary>
            public const byte ArrNumC2 = 0xF1;

            /// <summary>
            /// Array of 1-byte item count of uniform number arrays of 1-byte item count.
            /// </summary>
            public const byte ArrArrNumC1C1 = 0xF2;

            /// <summary>
            /// Array of 2-byte item count of uniform number arrays of 2-byte item count.
            /// </summary>
            public const byte ArrArrNumC2C2 = 0xF3;

            // <empty> 0xF4
            // <empty> 0xF5
            // <empty> 0xF6
            // <empty> 0xF7
            #endregion

            #region [0xF8, 0xFF]: Special Values
            // <special value reserved> 0xF8
            // <special value reserved> 0xF9
            // <special value reserved> 0xFA
            // <special value reserved> 0xFB
            // <special value reserved> 0xFC
            // <special value reserved> 0xFD
            // <special value reserved> 0xFE

            /// <summary>
            /// Type marker reserved to communicate an invalid type marker.
            /// </summary>
            public const byte Invalid = 0xFF;
            #endregion

            #region Number Type Marker Utility Functions
            /// <summary>
            /// Gets whether an integer can be encoded as a literal.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer can be encoded as a literal.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEncodedNumberLiteral(long value)
            {
                return InRange(value, LiteralIntMin, LiteralIntMax);
            }

            /// <summary>
            /// Gets whether an integer is a fixed length integer.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer is a fixed length integer.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsFixedLengthNumber(long value)
            {
                return InRange(value, NumberUInt8, NumberDouble + 1);
            }

            /// <summary>
            /// Gets whether an integer is a number.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer is a number.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsNumber(long value)
            {
                return IsEncodedNumberLiteral(value) || IsFixedLengthNumber(value);
            }

            /// <summary>
            /// Encodes an integer as a literal.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>The integer encoded as a literal if it can; else Invalid</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static byte EncodeIntegerLiteral(long value)
            {
                return IsEncodedNumberLiteral(value) ? (byte)(LiteralIntMin + value) : Invalid;
            }
            #endregion

            #region String Type Markers Utility Functions
            /// <summary>
            /// Gets whether a typeMarker is for a system string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a system string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsSystemString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString1ByteLengthMin, SystemString1ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a one byte encoded user string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a one byte encoded user string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsOneByteEncodedUserString(byte typeMarker)
            {
                return InRange(typeMarker, UserString1ByteLengthMin, UserString1ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a two byte encoded user string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a two byte encoded user string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsTwoByteEncodedUserString(byte typeMarker)
            {
                return InRange(typeMarker, UserString2ByteLengthMin, UserString2ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a user string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a user string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsUserString(byte typeMarker)
            {
                return IsOneByteEncodedUserString(typeMarker) || IsTwoByteEncodedUserString(typeMarker);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a one byte encoded string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a one byte encoded string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsOneByteEncodedString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString1ByteLengthMin, UserString1ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a two byte encoded string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a two byte encoded string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsTwoByteEncodedString(byte typeMarker)
            {
                return IsTwoByteEncodedUserString(typeMarker);
            }

            /// <summary>
            /// Gets whether a typeMarker is for an encoded string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for an encoded string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEncodedString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString1ByteLengthMin, UserString2ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for an encoded length string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for an encoded string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEncodedLengthString(byte typeMarker)
            {
                return InRange(typeMarker, EncodedStringLengthMin, EncodedStringLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a compressed string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a compressed string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsCompressedString(byte typeMarker) => InRange(typeMarker, CompressedLowercaseHexString, Packed7BitStringLength2 + 1);

            /// <summary>
            /// Gets whether a typeMarker is for a variable length string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a variable length string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsVariableLengthString(byte typeMarker) => IsEncodedLengthString(typeMarker) || InRange(typeMarker, String1ByteLength, String4ByteLength + 1);

            /// <summary>
            /// Gets whether a typeMarker is for a reference string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a reference string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsReferenceString(byte typeMarker) => InRange(typeMarker, ReferenceString1ByteOffset, ReferenceString4ByteOffset + 1);

            /// <summary>
            /// Gets whether a typeMarker is for a GUID string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a GUID string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsGuidString(byte typeMarker) => InRange(typeMarker, LowercaseGuidString, DoubleQuotedLowercaseGuidString + 1);

            /// <summary>
            /// Gets whether a typeMarker is for a hexadecimal string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a hexadecimal string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsHexadecimalString(byte typeMarker) => InRange(typeMarker, CompressedLowercaseHexString, CompressedUppercaseHexString + 1);

            /// <summary>
            /// Gets whether a typeMarker is for a datetime string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a datetime string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsDateTimeString(byte typeMarker) => typeMarker == CompressedDateTimeString;

            /// <summary>
            /// Gets whether a typeMarker is for a string.
            /// </summary>
            /// <param name="typeMarker">The type maker.</param>
            /// <returns>Whether the typeMarker is for a string.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsString(byte typeMarker) => InRange(typeMarker, SystemString1ByteLengthMin, UserString2ByteLengthMax)
                || InRange(typeMarker, LowercaseGuidString, ReferenceString4ByteOffset + 1);

            /// <summary>
            /// Gets the length of a encoded string type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>The length of the encoded string type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static long GetEncodedStringLength(byte typeMarker)
            {
                return typeMarker & (EncodedStringLengthMin - 1);
            }

            /// <summary>
            /// Gets the type marker for an encoded string of a particular length.
            /// </summary>
            /// <param name="length">The length of the encoded string.</param>
            /// <param name="typeMarker">The type marker for the encoded string of particular length if valid.</param>
            /// <returns>Whether or not the there is a typemarker for the string of a particular length.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool TryGetEncodedStringLengthTypeMarker(long length, out byte typeMarker)
            {
                if (length >= (EncodedStringLengthMax - EncodedStringLengthMin))
                {
                    typeMarker = default;
                    return false;
                }

                typeMarker = (byte)(length | EncodedStringLengthMin);
                return true;
            }
            #endregion

            #region Other Primitive Type Markers Utility Functions
            /// <summary>
            /// Gets whether a type maker is the null type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is the null type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsNull(byte typeMarker)
            {
                return typeMarker == Null;
            }

            /// <summary>
            /// Gets whether a type maker is the false type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is the false type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsFalse(byte typeMarker)
            {
                return typeMarker == False;
            }

            /// <summary>
            /// Gets whether a type maker is the true type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is the true type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsTrue(byte typeMarker)
            {
                return typeMarker == True;
            }

            /// <summary>
            /// Gets whether a type maker is a boolean type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is a boolean type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsBoolean(byte typeMarker)
            {
                return (typeMarker == False) || (typeMarker == True);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsGuid(byte typeMarker)
            {
                return typeMarker == Guid;
            }
            #endregion

            #region Array/Object Type Markers
            /// <summary>
            /// Gets whether a type marker is the empty array type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is the empty array type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEmptyArray(byte typeMarker)
            {
                return typeMarker == Arr0;
            }

            /// <summary>
            /// Gets whether a type marker is for an array.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is for an array.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsArray(byte typeMarker)
            {
                return InRange(typeMarker, Arr0, ArrLC4 + 1) ||
                    InRange(typeMarker, ArrNumC1, ArrArrNumC2C2 + 1);
            }

            /// <summary>
            /// Gets whether a type marker is the empty object type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is the empty object type marker.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsEmptyObject(byte typeMarker)
            {
                return typeMarker == Obj0;
            }

            /// <summary>
            /// Gets whether a type marker is for an object.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is for an object.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsObject(byte typeMarker)
            {
                return InRange(typeMarker, Obj0, ObjLC4 + 1);
            }
            #endregion

            #region Common Utility Functions
            /// <summary>
            /// Gets whether a type marker is valid.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is valid.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool IsValid(byte typeMarker)
            {
                return typeMarker != Invalid;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static bool InRange(long value, long minInclusive, long maxExclusive) => (value >= minInclusive) && (value < maxExclusive);
            #endregion
        }
    }
}
