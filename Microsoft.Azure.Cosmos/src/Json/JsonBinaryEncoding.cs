//-----------------------------------------------------------------------
// <copyright file="JsonBinaryEncoding.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Static class with utility functions and constants for JSON binary encoding.
    /// </summary>
    internal static class JsonBinaryEncoding
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
        /// List is system strings
        /// </summary>
        private static readonly string[] SystemStrings = new string[]
        {
            "$s",
            "$t",
            "$v",
            "_attachments",
            "_etag",
            "_rid",
            "_self",
            "_ts",
            "attachments/",
            "coordinates",
            "geometry",
            "GeometryCollection",
            "id",
            "inE",
            "inV",
            "label",
            "LineString",
            "link",
            "MultiLineString",
            "MultiPoint",
            "MultiPolygon",
            "name",
            "outE",
            "outV",
            "Point",
            "Polygon",
            "properties",
            "type",
            "value",
            "Feature",
            "FeatureCollection",
            "_id",
        };

        /// <summary>
        /// Dictionary of system string to it's index.
        /// </summary>
        private static readonly Dictionary<string, int> SystemStringToId = SystemStrings
            .Select((value, index) => new { value, index })
            .ToDictionary(pair => pair.value, pair => pair.index);

        /// <summary>
        /// Gets the number value from the binary reader.
        /// </summary>
        /// <param name="binaryReader">BinaryReader pointing to a number.</param>
        /// <returns>The number value from the binary reader.</returns>
        public static double GetNumberValue(BinaryReader binaryReader)
        {
            byte typeMarker = binaryReader.ReadByte();
            if (JsonBinaryEncoding.TypeMarker.IsEncodedIntegerLiteral(typeMarker))
            {
                return typeMarker - JsonBinaryEncoding.TypeMarker.LiteralIntMin;
            }

            switch (typeMarker)
            {
                case JsonBinaryEncoding.TypeMarker.UInt8:
                    return binaryReader.ReadByte();
                case JsonBinaryEncoding.TypeMarker.Int16:
                    return binaryReader.ReadInt16();
                case JsonBinaryEncoding.TypeMarker.Int32:
                    return binaryReader.ReadInt32();
                case JsonBinaryEncoding.TypeMarker.Int64:
                    return binaryReader.ReadInt64();
                case JsonBinaryEncoding.TypeMarker.Double:
                    return binaryReader.ReadDouble();
                default:
                    throw new JsonInvalidNumberException();
            }
        }

        /// <summary>
        /// Gets the string value from the binary reader.
        /// </summary>
        /// <param name="binaryReader">A binary reader whose cursor is at the beginning of a stream.</param>
        /// <returns>A string value from the binary reader.</returns>
        public static string GetStringValue(BinaryReader binaryReader)
        {
            byte typeMarker = binaryReader.ReadByte();
            binaryReader.BaseStream.Position--;
            string value;
            if (JsonBinaryEncoding.TypeMarker.IsSystemString(typeMarker))
            {
                value = JsonBinaryEncoding.GetEncodedSystemString(binaryReader);
            }
            else if (JsonBinaryEncoding.TypeMarker.IsUserString(typeMarker))
            {
                value = JsonBinaryEncoding.GetEncodedUserString(binaryReader);
            }
            else
            {
                // Retrieve utf-8 buffered string
                value = JsonBinaryEncoding.GetUTFString(binaryReader);
            }

            return value;
        }

        /// <summary>
        /// Try Get JsonTokenType
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>the JsonTokenType</returns>
        public static JsonTokenType TryGetJsonTokenType(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try Get Value Length
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>the ValueLength</returns>
        public static long TryGetValueLength(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try Get NumberValue
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>the NumberValue</returns>
        public static double TryGetNumberValue(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try Get Encoded String Type Marker
        /// </summary>
        /// <param name="value">the value</param>
        /// <returns>Encoded String Type Marker</returns>
        public static MultiByteTypeMarker TryGetEncodedStringTypeMarker(string value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try Get Encoded String Value
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>Encoded String Value</returns>
        public static string TryGetEncodedStringValue(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try Get Buffered String Value
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>Buffered String Value</returns>
        public static IReadOnlyList<byte> TryGetBufferedStringValue(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try Get String Value
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>String Value</returns>
        public static string TryGetStringValue(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try Get Encoded System String Type Marker
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>Encoded System String Type Marker</returns>
        public static MultiByteTypeMarker TryGetEncodedSystemStringTypeMarker(string value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the node type of a type marker.
        /// </summary>
        /// <param name="typeMarker">The type maker as input.</param>
        /// <returns>the node type of the type marker.</returns>
        public static JsonNodeType GetNodeType(byte typeMarker)
        {
            return JsonBinaryEncoding.ValueTypes.Types[typeMarker];
        }

        /// <summary>
        /// Gets the length of a particular value given it's typemarker
        /// </summary>
        /// <param name="buffer">The buffer to read from as input.</param>
        /// <param name="offset">The offset to read from as input.</param>
        /// <returns>
        /// - Positive Value: The length of the value including its TypeMarker
        /// - Negative Value: The length is encoded as an integer of size equals to abs(value) following the TypeMarker byte
        /// - Zero Value: The length is unknown (for instance an unassigned type marker)
        /// </returns>
        public static long GetValueLength(byte[] buffer, long offset)
        {
            return JsonBinaryEncoding.ValueLengths.GetValueLength(buffer, (int)offset);
        }

        /// <summary>
        /// Gets the length of a particular string given it's typemarker.
        /// </summary>
        /// <param name="typeMarker">The type marker as input</param>
        /// <returns>
        /// - Non-Negative Value: The TypeMarker encodes the string length
        /// - Negative Value: System or user dictionary encoded string, or encoded string length that follows the TypeMarker
        /// </returns>
        public static int GetStringLengths(byte typeMarker)
        {
            return JsonBinaryEncoding.StringLengths.Lengths[typeMarker];
        }

        /// <summary>
        /// Gets the offset of the first item in an array or object
        /// </summary>
        /// <param name="typeMarker">The typemarker as input.</param>
        /// <returns>The offset of the first item in an array or object</returns>
        public static int GetFirstValueOffset(byte typeMarker)
        {
            return JsonBinaryEncoding.FirstValueOffsets.Offsets[typeMarker];
        }

        /// <summary>
        /// Gets a system string by ID.
        /// </summary>
        /// <param name="id">The SystemStringId.</param>
        /// <returns>The system string for the id.</returns>
        public static string GetSystemStringById(int id)
        {
            return JsonBinaryEncoding.SystemStrings[id];
        }

        /// <summary>
        /// Gets the SystemStringId for a particular system string.
        /// </summary>
        /// <param name="systemString">The system string to get the enum id for.</param>
        /// <param name="systemStringId">The id of the system string if found.</param>
        /// <returns>The SystemStringId for a particular system string.</returns>
        public static bool TryGetSystemStringId(string systemString, out int systemStringId)
        {
            return JsonBinaryEncoding.SystemStringToId.TryGetValue(systemString, out systemStringId);
        }

        /// <summary>
        /// Try Get Encoded User String Type Marker
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>Encoded User String Type Marker</returns>
        private static MultiByteTypeMarker TryGetEncodedUserStringTypeMarker(string value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try Get Encoded System String Value
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>Encoded System String Value</returns>
        private static string TryGetEncodedSystemStringValue(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Try Get Encoded User String Value
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        /// <returns>Encoded User String Value</returns>
        private static string TryGetEncodedUserStringValue(byte[] buffer)
        {
            throw new NotImplementedException();
        }

        private static string GetEncodedUserString(BinaryReader binaryReader)
        {
            throw new NotImplementedException();
        }

        private static string GetEncodedSystemString(BinaryReader binaryReader)
        {
            byte typeMarker = binaryReader.ReadByte();
            int systemStringId;
            if (JsonBinaryEncoding.TypeMarker.IsOneByteEncodedSystemString(typeMarker))
            {
                systemStringId = typeMarker - JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin;
            }
            else if (JsonBinaryEncoding.TypeMarker.IsTwoByteEncodedSystemString(typeMarker))
            {
                byte firstByte = typeMarker;
                byte secondByte = binaryReader.ReadByte();

                systemStringId = ((firstByte - JsonBinaryEncoding.TypeMarker.SystemString2ByteLengthMin) * 0xFF) + secondByte;
            }
            else
            {
                throw new JsonNotStringTokenException();
            }

            return JsonBinaryEncoding.GetSystemStringById(systemStringId);
        }

        private static string GetStringFromReader(BinaryReader binaryReader, long length)
        {
            if (length > int.MaxValue)
            {
                throw new InvalidOperationException("Can not get a string value that is greater than int.MaxValue");
            }

            // Note that all string in binary encoding is UTF8
            MemoryStream memoryStream = binaryReader.BaseStream as MemoryStream;
            byte[] buffer;
            int offset;
            if (memoryStream != null)
            {
                buffer = memoryStream.GetBuffer();
                offset = (int)binaryReader.BaseStream.Position;
            }
            else
            {
                buffer = binaryReader.ReadBytes((int)length);
                offset = 0;
            }

            return Encoding.UTF8.GetString(buffer, offset, (int)length);
        }

        private static string GetUTFString(BinaryReader binaryReader)
        {
            byte typeMarker = binaryReader.ReadByte();
            long length;
            if (JsonBinaryEncoding.TypeMarker.IsEncodedLengthString(typeMarker))
            {
                length = JsonBinaryEncoding.GetStringLengths(typeMarker);
            }
            else
            {
                switch (typeMarker)
                {
                    case JsonBinaryEncoding.TypeMarker.String1ByteLength:
                        length = binaryReader.ReadByte();
                        break;
                    case JsonBinaryEncoding.TypeMarker.String2ByteLength:
                        length = binaryReader.ReadUInt16();
                        break;
                    case JsonBinaryEncoding.TypeMarker.String4ByteLength:
                        length = binaryReader.ReadUInt32();
                        break;
                    default:
                        throw new JsonNotStringTokenException();
                }
            }

            return JsonBinaryEncoding.GetStringFromReader(binaryReader, length);
        }

        /// <summary>
        /// Defines the set of type-marker values that are used to encode JSON value
        /// </summary>
        public struct TypeMarker
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

            #region [0x60, 0x68): Encoded 2-byte system string (8 values)
            /// <summary>
            /// The first type marker for a system string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte SystemString2ByteLengthMin = UserString1ByteLengthMax;

            /// <summary>
            /// The last type marker for a system string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte SystemString2ByteLengthMax = SystemString2ByteLengthMin + 8;
            #endregion

            #region [0x68, 0x80): Encoded 2-byte user string (24 values)
            /// <summary>
            /// The first type marker for a user string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte UserString2ByteLengthMin = SystemString2ByteLengthMax;

            /// <summary>
            /// The last type marker for a user string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte UserString2ByteLengthMax = UserString2ByteLengthMin + 24;
            #endregion

            #region [0x80, 0xC0): Encoded string length (64 values)
            /// <summary>
            /// The first type marker for a string whose length is encoded.
            /// </summary>
            /// <example>EncodedStringLengthMin + 1 is a type marker for a string with length 1.</example>
            public const byte EncodedStringLengthMin = UserString2ByteLengthMax;

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
            /// Type marker for a Compressed string of 1-byte length
            /// </summary>
            public const byte CompressedString1ByteLength = 0xC3;

            /// <summary>
            /// Type marker for a Compressed string of 2-byte length
            /// </summary>
            public const byte CompressedString2ByteLength = 0xC4;

            /// <summary>
            /// Type marker for a Compressed string of 4-byte length
            /// </summary>
            public const byte CompressedString4ByteLength = 0xC5;

            // <string reserved> 0xC6
            // <string reserved> 0xC7
            #endregion

            #region [0xC8, 0xD0): Number Values
            /// <summary>
            /// Type marker for a 1-byte unsigned integer
            /// </summary>
            public const byte UInt8 = 0xC8;

            /// <summary>
            /// Type marker for a 2-byte singed integer
            /// </summary>
            public const byte Int16 = 0xC9;

            /// <summary>
            /// Type marker for a 4-byte singed integer
            /// </summary>
            public const byte Int32 = 0xCA;

            /// <summary>
            /// Type marker for a 8-byte singed integer
            /// </summary>
            public const byte Int64 = 0xCB;

            /// <summary>
            /// Type marker for a Double-precession floating point number
            /// </summary>
            public const byte Double = 0xCC;

            // <number reserved> 0xCD
            // <number reserved> 0xCE
            // <number reserved> 0xCF
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

            // <other types reserved> 0xD3
            // <other types reserved> 0xD4
            // <other types reserved> 0xD5
            // <other types reserved> 0xD6
            // <other types reserved> 0xD7

            // <other types reserved> 0xD8
            // <other types reserved> 0xD9
            // <other types reserved> 0xDA
            // <other types reserved> 0xDB
            // <other types reserved> 0xDC
            // <other types reserved> 0xDD
            // <other types reserved> 0xDE
            // <other types reserved> 0xDF
            #endregion

            #region [0xEO, 0xE8): Array Type Markers

            /// <summary>
            /// Empty array type marker.
            /// </summary>
            public const byte EmptyArray = 0xE0;

            /// <summary>
            /// Single-item array type marker.
            /// </summary>
            public const byte SingleItemArray = 0xE1;

            /// <summary>
            /// Array of 1-byte length type marker.
            /// </summary>
            public const byte Array1ByteLength = 0xE2;

            /// <summary>
            /// Array of 2-byte length type marker.
            /// </summary>
            public const byte Array2ByteLength = 0xE3;

            /// <summary>
            /// Array of 4-byte length type marker.
            /// </summary>
            public const byte Array4ByteLength = 0xE4;

            /// <summary>
            /// Array of 1-byte length and item count type marker.
            /// </summary>
            public const byte Array1ByteLengthAndCount = 0xE5;

            /// <summary>
            /// Array of 2-byte length and item count type marker.
            /// </summary>
            public const byte Array2ByteLengthAndCount = 0xE6;

            /// <summary>
            /// Array of 4-byte length and item count type marker.
            /// </summary>
            public const byte Array4ByteLengthAndCount = 0xE7;
            #endregion

            #region [0xE8, 0xF0): Object Type Markers
            /// <summary>
            /// Empty object type marker.
            /// </summary>
            public const byte EmptyObject = 0xE8;

            /// <summary>
            /// Single-property object type marker.
            /// </summary>
            public const byte SinglePropertyObject = 0xE9;

            /// <summary>
            /// Object of 1-byte length type marker.
            /// </summary>
            public const byte Object1ByteLength = 0xEA;

            /// <summary>
            /// Object of 2-byte length type marker.
            /// </summary>
            public const byte Object2ByteLength = 0xEB;

            /// <summary>
            /// Object of 4-byte length type maker.
            /// </summary>
            public const byte Object4ByteLength = 0xEC;

            /// <summary>
            /// Object of 1-byte length and property count type marker.
            /// </summary>
            public const byte Object1ByteLengthAndCount = 0xED;

            /// <summary>
            /// Object of 2-byte length and property count type marker.
            /// </summary>
            public const byte Object2ByteLengthAndCount = 0xEE;

            /// <summary>
            /// Object of 4-byte length and property count type marker.
            /// </summary>
            public const byte Object4ByteLengthAndCount = 0xEF;
            #endregion

            #region [0xF0, 0xF8): Empty Range
            // <empty> 0xF0
            // <empty> 0xF1
            // <empty> 0xF2
            // <empty> 0xF3
            // <empty> 0xF4
            // <empty> 0xF5
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
            public static bool IsEncodedIntegerLiteral(long value)
            {
                return InRange(value, LiteralIntMin, LiteralIntMax);
            }

            /// <summary>
            /// Gets whether an integer is a fixed length integer.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer is a fixed length integer.</returns>
            public static bool IsFixedLengthInteger(long value)
            {
                return InRange(value, UInt8, Double + 1);
            }

            /// <summary>
            /// Gets whether an integer is a number.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer is a number.</returns>
            public static bool IsNumber(long value)
            {
                return IsEncodedIntegerLiteral(value) || IsFixedLengthInteger(value);
            }

            /// <summary>
            /// Encodes an integer as a literal.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>The integer encoded as a literal if it can; else Invalid</returns>
            public static byte EncodeIntegerLiteral(long value)
            {
                return IsEncodedIntegerLiteral(value) ? (byte)(LiteralIntMin + value) : Invalid;
            }
            #endregion

            #region String Type Markers Utility Functions
            /// <summary>
            /// Gets whether a typeMarker is for a one byte encoded system string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a one byte encoded system string.</returns>
            public static bool IsOneByteEncodedSystemString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString1ByteLengthMin, SystemString1ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a two byte encoded system string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a two byte encoded system string.</returns>
            public static bool IsTwoByteEncodedSystemString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString2ByteLengthMin, SystemString2ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a system string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a system string.</returns>
            public static bool IsSystemString(byte typeMarker)
            {
                return IsOneByteEncodedSystemString(typeMarker) || IsTwoByteEncodedSystemString(typeMarker);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a one byte encoded user string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a one byte encoded user string.</returns>
            public static bool IsOneByteEncodedUserString(byte typeMarker)
            {
                return InRange(typeMarker, UserString1ByteLengthMin, UserString1ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a two byte encoded user string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a two byte encoded user string.</returns>
            public static bool IsTwoByteEncodedUserString(byte typeMarker)
            {
                return InRange(typeMarker, UserString2ByteLengthMin, UserString2ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a user string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a user string.</returns>
            public static bool IsUserString(byte typeMarker)
            {
                return IsOneByteEncodedUserString(typeMarker) || IsTwoByteEncodedUserString(typeMarker);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a one byte encoded string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a one byte encoded string.</returns>
            public static bool IsOneByteEncodedString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString1ByteLengthMin, UserString1ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a two byte encoded string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a two byte encoded string.</returns>
            public static bool IsTwoByteEncodedString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString2ByteLengthMin, UserString2ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for an encoded string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for an encoded string.</returns>
            public static bool IsEncodedString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString1ByteLengthMin, UserString2ByteLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for an encoded length string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for an encoded string.</returns>
            public static bool IsEncodedLengthString(byte typeMarker)
            {
                return InRange(typeMarker, EncodedStringLengthMin, EncodedStringLengthMax);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a variable length string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a variable length string.</returns>
            public static bool IsVarLengthString(byte typeMarker)
            {
                return InRange(typeMarker, String1ByteLength, String4ByteLength + 1);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a variable length compressed string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a variable length compressed string.</returns>
            public static bool IsVarLengthCompressedString(byte typeMarker)
            {
                return InRange(typeMarker, CompressedString1ByteLength, CompressedString4ByteLength + 1);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a string.
            /// </summary>
            /// <param name="typeMarker">The type maker.</param>
            /// <returns>Whether the typeMarker is for a string.</returns>
            public static bool IsString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString1ByteLengthMin, CompressedString4ByteLength + 1);
            }

            /// <summary>
            /// Gets the length of a encoded string type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>The length of the encoded string type marker.</returns>
            public static long GetEncodedStringLength(byte typeMarker)
            {
                return typeMarker & (EncodedStringLengthMin - 1);
            }

            /// <summary>
            /// Gets the type marker for an encoded string of a particular length.
            /// </summary>
            /// <param name="length">The length of the encoded string.</param>
            /// <returns>The type marker for an encoded string of a particular length.</returns>
            public static byte GetEncodedStringLengthTypeMarker(long length)
            {
                return length < (EncodedStringLengthMax - EncodedStringLengthMin) ? (byte)(length | EncodedStringLengthMin) : Invalid;
            }
            #endregion

            #region Other Primitive Type Markers Utility Functions
            /// <summary>
            /// Gets whether a type maker is the null type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is the null type marker.</returns>
            public static bool IsNull(byte typeMarker)
            {
                return typeMarker == Null;
            }

            /// <summary>
            /// Gets whether a type maker is the false type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is the false type marker.</returns>
            public static bool IsFalse(byte typeMarker)
            {
                return typeMarker == False;
            }

            /// <summary>
            /// Gets whether a type maker is the true type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is the true type marker.</returns>
            public static bool IsTrue(byte typeMarker)
            {
                return typeMarker == True;
            }

            /// <summary>
            /// Gets whether a type maker is a boolean type marker.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type maker is a boolean type marker.</returns>
            public static bool IsBoolean(byte typeMarker)
            {
                return (typeMarker == False) || (typeMarker == True);
            }

            // Array/Object Type Markers

            /// <summary>
            /// Gets whether a type marker is for an array.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is for an array.</returns>
            public static bool IsArray(byte typeMarker)
            {
                return InRange(typeMarker, EmptyArray, Array4ByteLengthAndCount + 1);
            }

            /// <summary>
            /// Gets whether a type marker is for an object.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is for an object.</returns>
            public static bool IsObject(byte typeMarker)
            {
                return InRange(typeMarker, EmptyObject, Object4ByteLengthAndCount + 1);
            }
            #endregion

            #region Common Utility Functions
            /// <summary>
            /// Gets whether a type marker is valid.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the type marker is valid.</returns>
            public static bool IsValid(byte typeMarker)
            {
                return typeMarker != Invalid;
            }
            #endregion

            private static bool InRange(long value, long minInclusive, long maxExclusive)
            {
                return (value >= minInclusive) && (value < maxExclusive);
            }
        }

        /// <summary>
        /// Struct to hold the a multibyte type marker.
        /// </summary>
        public struct MultiByteTypeMarker
        {
            private readonly byte[] values;

            /// <summary>
            /// Initializes a new instance of the MultiByteTypeMarker struct.
            /// </summary>
            /// <param name="values">The payload for the multibyte type marker.</param>
            public MultiByteTypeMarker(byte[] values)
            {
                this.values = values;
            }

            /// <summary>
            /// Gets a the actual multibyte type marker.
            /// </summary>
            public byte[] Values
            {
                get
                {
                    return this.values;
                }
            }
        }

        private static class ValueTypes
        {
            private const JsonNodeType Null = JsonNodeType.Null;
            private const JsonNodeType False = JsonNodeType.False;
            private const JsonNodeType True = JsonNodeType.True;
            private const JsonNodeType Number = JsonNodeType.Number;
            private const JsonNodeType String = JsonNodeType.String;
            private const JsonNodeType Array = JsonNodeType.Array;
            private const JsonNodeType Object = JsonNodeType.Object;
            private const JsonNodeType Unknown = JsonNodeType.Unknown;

            private static JsonNodeType[] types =
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

                // Encoded 2-byte system string (8 values)
                String, String, String, String, String, String, String, String,

                // Encoded 2-byte user string (24 values)
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

                // Variable Length String Values
                String,     // StrL1 (1-byte length)
                String,     // StrL2 (2-byte length)
                String,     // StrL4 (4-byte length)
                String,     // CStrL1 (1-byte compressed length and actual length)
                String,     // CStrL2 (2-byte compressed length and actual length)
                String,     // CStrL4 (4-byte compressed length and actual length)
                Unknown,    // <string reserved> 0xC6
                Unknown,    // <string reserved> 0xC7

                // Number Values
                Number,     // UInt8
                Number,     // Int16,
                Number,     // Int32,
                Number,     // Int64,
                Number,     // Double,
                Unknown,    // <number reserved> 0xCD
                Unknown,    // <number reserved> 0xCE
                Unknown,    // <number reserved> 0xCF

                // Other Value Types
                Null,       // Null
                False,      // False
                True,       // True
                Unknown,    // <other types reserved> 0xD3
                Unknown,    // <other types reserved> 0xD4
                Unknown,    // <other types reserved> 0xD5
                Unknown,    // <other types reserved> 0xD6
                Unknown,    // <other types reserved> 0xD7

                Unknown,    // <other types reserved> 0xD8
                Unknown,    // <other types reserved> 0xD9
                Unknown,    // <other types reserved> 0xDA
                Unknown,    // <other types reserved> 0xDB
                Unknown,    // <other types reserved> 0xDC
                Unknown,    // <other types reserved> 0xDD
                Unknown,    // <other types reserved> 0xDE
                Unknown,    // <other types reserved> 0xDF

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

            public static IReadOnlyList<JsonNodeType> Types
            {
                get
                {
                    return ValueTypes.types;
                }
            }
        }

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
            private static int[] lengths =
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

                // Encoded 2-byte system string (8 values)
                2, 2, 2, 2, 2, 2, 2, 2, 
    
                // Encoded 2-byte user string (24 values)
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

                // Variable Length String Values
                L1,     // StrL1 (1-byte length)
                L2,     // StrL2 (2-byte length)
                L4,     // StrL4 (4-byte length)
                LC1,    // CStrL1 (1-byte compressed length and actual length)
                LC2,    // CStrL2 (2-byte compressed length and actual length)
                LC4,    // CStrL4 (4-byte compressed length and actual length)
                0,      // <string reserved> 0xC6
                0,      // <string reserved> 0xC7

                // Number Values
                2,      // UInt8
                3,      // Int16,
                5,      // Int32,
                9,      // Int64,
                9,      // Double,
                0,      // <number reserved> 0xCD
                0,      // <number reserved> 0xCE
                0,      // <number reserved> 0xCF

                // Other Value Types
                1,      // Null
                1,      // False
                1,      // True
                0,      // <other types reserved> 0xD3
                0,      // <other types reserved> 0xD4
                0,      // <other types reserved> 0xD5
                0,      // <other types reserved> 0xD6
                0,      // <other types reserved> 0xD7

                0,      // <other types reserved> 0xD8
                0,      // <other types reserved> 0xD9
                0,      // <other types reserved> 0xDA
                0,      // <other types reserved> 0xDB
                0,      // <other types reserved> 0xDC
                0,      // <other types reserved> 0xDD
                0,      // <other types reserved> 0xDE
                0,      // <other types reserved> 0xDF

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

            public static long GetValueLength(byte[] buffer, int offset)
            {
                long length = ValueLengths.lengths[buffer[offset]];
                if (length < 0)
                {
                    // Length was negative meaning we need to look into the buffer to find the length
                    switch (length)
                    {
                        case L1:
                            length = TypeMarkerLength + OneByteLength + buffer[offset + 1];
                            break;
                        case L2:
                            length = TypeMarkerLength + TwoByteLength + BitConverter.ToUInt16(buffer, offset + 1);
                            break;
                        case L4:
                            length = TypeMarkerLength + FourByteLength + BitConverter.ToUInt32(buffer, offset + 1);
                            break;
                        case LC1:
                            length = TypeMarkerLength + OneByteLength + OneByteCount + buffer[offset + 1];
                            break;
                        case LC2:
                            length = TypeMarkerLength + TwoByteLength + TwoByteCount + BitConverter.ToUInt16(buffer, offset + 1);
                            break;
                        case LC4:
                            length = TypeMarkerLength + FourByteLength + FourByteCount + BitConverter.ToUInt32(buffer, offset + 1);
                            break;
                        case Arr1:
                            long arrayOneItemLength = ValueLengths.GetValueLength(buffer, offset + 1);
                            length = arrayOneItemLength == 0 ? 0 : 1 + arrayOneItemLength;
                            break;
                        case Obj1:
                            long nameLength = ValueLengths.GetValueLength(buffer, offset + 1);
                            if (nameLength == 0)
                            {
                                length = 0;
                            }
                            else
                            {
                                long valueLength = ValueLengths.GetValueLength(buffer, offset + 1 + (int)nameLength);
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

        private static class StringLengths
        {
            private const int SysStr1 = -1;
            private const int UsrStr1 = -2;
            private const int SysStr2 = -3;
            private const int UsrStr2 = -4;
            private const int StrL1 = -5;
            private const int StrL2 = -6;
            private const int StrL4 = -7;
            private const int CStrL1 = -8;
            private const int CStrL2 = -9;
            private const int CStrL4 = -10;
            private const int NotStr = -11;

            /// <summary>
            /// Lookup table for encoded string length for each TypeMarker value (0 to 255)
            /// The lengths are encoded as follows:
            /// - Non-Negative Value: The TypeMarker encodes the string length
            /// - Negative Value: System or user dictionary encoded string, or encoded string length that follows the TypeMarker
            /// </summary>
            private static int[] lengths =
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

                // Encoded 2-byte system string (8 values) 
                SysStr2, SysStr2, SysStr2, SysStr2, SysStr2, SysStr2, SysStr2, SysStr2, 
    
                // Encoded 2-byte user string (24 values) 
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

                // Variable Length String Values
                StrL1,      // StrL1 (1-byte length)
                StrL2,      // StrL2 (2-byte length)
                StrL4,      // StrL4 (4-byte length)
                CStrL1,     // CStrL1 (1-byte compressed length and actual length)
                CStrL2,     // CStrL2 (2-byte compressed length and actual length)
                CStrL4,     // CStrL4 (4-byte compressed length and actual length)
                NotStr,     // <string reserved> 0xC6
                NotStr,     // <string reserved> 0xC7

                // Number Values
                NotStr,     // UInt8
                NotStr,     // Int16,
                NotStr,     // Int32,
                NotStr,     // Int64,
                NotStr,     // Double,
                NotStr,     // <number reserved> 0xCD
                NotStr,     // <number reserved> 0xCE
                NotStr,     // <number reserved> 0xCF

                // Other Value Types
                NotStr,     // Null
                NotStr,     // False
                NotStr,     // True
                NotStr,     // <other types reserved> 0xD3
                NotStr,     // <other types reserved> 0xD4
                NotStr,     // <other types reserved> 0xD5
                NotStr,     // <other types reserved> 0xD6
                NotStr,     // <other types reserved> 0xD7

                NotStr,     // <other types reserved> 0xD8
                NotStr,     // <other types reserved> 0xD9
                NotStr,     // <other types reserved> 0xDA
                NotStr,     // <other types reserved> 0xDB
                NotStr,     // <other types reserved> 0xDC
                NotStr,     // <other types reserved> 0xDD
                NotStr,     // <other types reserved> 0xDE
                NotStr,     // <other types reserved> 0xDF

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

        private static class FirstValueOffsets
        {
            /// <summary>
            /// Defines the offset of the first item in an array or object
            /// </summary>
            private static int[] offsets =
            {
                // Encoded literal integer value (32 values)
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,

                // Encoded 0-byte system string (32 values)
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,

                // Encoded 0-byte user string (32 values)
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,

                // Encoded 2-byte system string (8 values)
                0, 0, 0, 0, 0, 0, 0, 0, 
    
                // Encoded 2-byte user string (24 values)
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0,

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
                0,      // StrL1 (1-byte length)
                0,      // StrL2 (2-byte length)
                0,      // StrL4 (4-byte length)
                0,      // CStrL1 (1-byte compressed length and actual length)
                0,      // CStrL2 (2-byte compressed length and actual length)
                0,      // CStrL4 (4-byte compressed length and actual length)
                0,      // <string reserved> 0xC6
                0,      // <string reserved> 0xC7

                // Number Values
                0,      // UInt8
                0,      // Int16,
                0,      // Int32,
                0,      // Int64,
                0,      // Double,
                0,      // <number reserved> 0xCD
                0,      // <number reserved> 0xCE
                0,      // <number reserved> 0xCF

                // Other Value Types
                0,      // Null
                0,      // False
                0,      // True
                0,      // <other types reserved> 0xD3
                0,      // <other types reserved> 0xD4
                0,      // <other types reserved> 0xD5
                0,      // <other types reserved> 0xD6
                0,      // <other types reserved> 0xD7

                0,      // <other types reserved> 0xD8
                0,      // <other types reserved> 0xD9
                0,      // <other types reserved> 0xDA
                0,      // <other types reserved> 0xDB
                0,      // <other types reserved> 0xDC
                0,      // <other types reserved> 0xDD
                0,      // <other types reserved> 0xDE
                0,      // <other types reserved> 0xDF

                // Array Type Markers
                1,      // Arr0
                1,      // Arr1
                2,      // ArrL1 (1-byte length)
                3,      // ArrL2 (2-byte length)
                5,      // ArrL4 (4-byte length)
                3,      // ArrLC1 (1-byte length and count)
                5,      // ArrLC2 (2-byte length and count)
                9,      // ArrLC4 (4-byte length and count)

                // Object Type Markers
                1,      // Obj0
                1,      // Obj1
                2,      // ObjL1 (1-byte length)
                3,      // ObjL2 (2-byte length)
                5,      // ObjL4 (4-byte length)
                3,      // ObjLC1 (1-byte length and count)
                5,      // ObjLC2 (2-byte length and count)
                9,      // ObjLC4 (4-byte length and count)

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

            public static IReadOnlyList<int> Offsets
            {
                get
                {
                    return FirstValueOffsets.offsets;
                }
            }
        }
    }
}
