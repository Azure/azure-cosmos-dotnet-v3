//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
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
        /// <param name="numberToken">The buffer to read the number from.</param>
        /// <returns>The number value from the binary reader.</returns>
        public static Number64 GetNumberValue(ReadOnlySpan<byte> numberToken)
        {
            if (!JsonBinaryEncoding.TryGetNumberValue(numberToken, out Number64 number64, out int bytesConsumed))
            {
                throw new JsonNotNumberTokenException();
            }

            return number64;
        }

        /// <summary>
        /// Try Get NumberValue
        /// </summary>
        /// <param name="numberToken">The buffer.</param>
        /// <param name="number64">The number.</param>
        /// <param name="bytesConsumed">The number of bytes consumed</param>
        /// <returns>Whether a number was parsed.</returns>
        public static bool TryGetNumberValue(ReadOnlySpan<byte> numberToken, out Number64 number64, out int bytesConsumed)
        {
            number64 = 0;
            bytesConsumed = 0;

            if (numberToken.Length == 0)
            {
                return false;
            }

            byte typeMarker = numberToken[0];

            if (JsonBinaryEncoding.TypeMarker.IsEncodedNumberLiteral(typeMarker))
            {
                number64 = typeMarker - JsonBinaryEncoding.TypeMarker.LiteralIntMin;
                bytesConsumed = 1;
            }
            else
            {
                switch (typeMarker)
                {
                    case JsonBinaryEncoding.TypeMarker.NumberUInt8:
                        if (numberToken.Length < (1 + 1))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<byte>(numberToken.Slice(1));
                        bytesConsumed = 1 + 1;
                        break;

                    case JsonBinaryEncoding.TypeMarker.NumberInt16:
                        if (numberToken.Length < (1 + 2))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<short>(numberToken.Slice(1));
                        bytesConsumed = 1 + 2;
                        break;

                    case JsonBinaryEncoding.TypeMarker.NumberInt32:
                        if (numberToken.Length < (1 + 4))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<int>(numberToken.Slice(1));
                        bytesConsumed = 1 + 4;
                        break;

                    case JsonBinaryEncoding.TypeMarker.NumberInt64:
                        if (numberToken.Length < (1 + 8))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<long>(numberToken.Slice(1));
                        bytesConsumed = 1 + 8;
                        break;

                    case JsonBinaryEncoding.TypeMarker.NumberDouble:
                        if (numberToken.Length < (1 + 8))
                        {
                            return false;
                        }

                        number64 = MemoryMarshal.Read<double>(numberToken.Slice(1));
                        bytesConsumed = 1 + 8;
                        break;

                    default:
                        throw new JsonInvalidNumberException();
                }
            }

            return true;
        }

        public static sbyte GetInt8Value(ReadOnlySpan<byte> int8Token)
        {
            if (!JsonBinaryEncoding.TryGetInt8Value(int8Token, out sbyte int8Value))
            {
                throw new JsonInvalidNumberException();
            }

            return int8Value;
        }

        public static bool TryGetInt8Value(
            ReadOnlySpan<byte> int8Token,
            out sbyte int8Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<sbyte>(
                int8Token,
                JsonBinaryEncoding.TypeMarker.Int8,
                out int8Value);
        }

        public static short GetInt16Value(ReadOnlySpan<byte> int16Token)
        {
            if (!JsonBinaryEncoding.TryGetInt16Value(int16Token, out short int16Value))
            {
                throw new JsonInvalidNumberException();
            }

            return int16Value;
        }

        public static bool TryGetInt16Value(
            ReadOnlySpan<byte> int16Token,
            out short int16Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<short>(
                int16Token,
                JsonBinaryEncoding.TypeMarker.Int16,
                out int16Value);
        }

        public static int GetInt32Value(ReadOnlySpan<byte> int32Token)
        {
            if (!JsonBinaryEncoding.TryGetInt32Value(int32Token, out int int32Value))
            {
                throw new JsonInvalidNumberException();
            }

            return int32Value;
        }

        public static bool TryGetInt32Value(
            ReadOnlySpan<byte> int32Token,
            out int int32Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<int>(
                int32Token,
                JsonBinaryEncoding.TypeMarker.Int32,
                out int32Value);
        }

        public static long GetInt64Value(ReadOnlySpan<byte> int64Token)
        {
            if (!JsonBinaryEncoding.TryGetInt64Value(int64Token, out long int64Value))
            {
                throw new JsonInvalidNumberException();
            }

            return int64Value;
        }

        public static bool TryGetInt64Value(
            ReadOnlySpan<byte> int64Token,
            out long int64Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<long>(
                int64Token,
                JsonBinaryEncoding.TypeMarker.Int64,
                out int64Value);
        }

        public static uint GetUInt32Value(ReadOnlySpan<byte> uInt32Token)
        {
            if (!JsonBinaryEncoding.TryGetUInt32Value(uInt32Token, out uint uInt32Value))
            {
                throw new JsonInvalidNumberException();
            }

            return uInt32Value;
        }

        public static bool TryGetUInt32Value(
            ReadOnlySpan<byte> uInt32Token,
            out uint uInt32Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<uint>(
                uInt32Token,
                JsonBinaryEncoding.TypeMarker.UInt32,
                out uInt32Value);
        }

        public static float GetFloat32Value(ReadOnlySpan<byte> float32Token)
        {
            if (!JsonBinaryEncoding.TryGetFloat32Value(float32Token, out float float32Value))
            {
                throw new JsonInvalidNumberException();
            }

            return float32Value;
        }

        public static bool TryGetFloat32Value(
            ReadOnlySpan<byte> float32Token,
            out float float32Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<float>(
                float32Token,
                JsonBinaryEncoding.TypeMarker.Float32,
                out float32Value);
        }

        public static double GetFloat64Value(ReadOnlySpan<byte> float64Token)
        {
            if (!JsonBinaryEncoding.TryGetFloat64Value(float64Token, out double float64Value))
            {
                throw new JsonInvalidNumberException();
            }

            return float64Value;
        }

        public static bool TryGetFloat64Value(
            ReadOnlySpan<byte> float64Token,
            out double float64Value)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<double>(
                float64Token,
                JsonBinaryEncoding.TypeMarker.Float64,
                out float64Value);
        }

        public static Guid GetGuidValue(ReadOnlySpan<byte> guidToken)
        {
            if (!JsonBinaryEncoding.TryGetGuidValue(guidToken, out Guid guidValue))
            {
                throw new JsonInvalidNumberException();
            }

            return guidValue;
        }

        public static bool TryGetGuidValue(
            ReadOnlySpan<byte> guidToken,
            out Guid guidValue)
        {
            return JsonBinaryEncoding.TryGetFixedWidthValue<Guid>(
                guidToken,
                JsonBinaryEncoding.TypeMarker.Guid,
                out guidValue);
        }

        public static ReadOnlyMemory<byte> GetBinaryValue(ReadOnlyMemory<byte> binaryToken)
        {
            if (!JsonBinaryEncoding.TryGetBinaryValue(binaryToken, out ReadOnlyMemory<byte> binaryValue))
            {
                throw new JsonInvalidTokenException();
            }

            return binaryValue;
        }

        public static bool TryGetBinaryValue(
            ReadOnlyMemory<byte> binaryToken,
            out ReadOnlyMemory<byte> binaryValue)
        {
            binaryValue = default;
            if (binaryToken.Length == 0)
            {
                return false;
            }

            byte typeMarker = binaryToken.Span[0];
            uint length;
            switch (typeMarker)
            {
                case JsonBinaryEncoding.TypeMarker.Binary1ByteLength:
                    if (binaryToken.Length < (1 + 1))
                    {
                        return false;
                    }

                    length = MemoryMarshal.Read<byte>(binaryToken.Span.Slice(1));
                    break;

                case JsonBinaryEncoding.TypeMarker.Binary2ByteLength:
                    if (binaryToken.Length < (1 + 2))
                    {
                        return false;
                    }

                    length = MemoryMarshal.Read<ushort>(binaryToken.Span.Slice(1));
                    break;

                case JsonBinaryEncoding.TypeMarker.Binary4ByteLength:
                    if (binaryToken.Length < (1 + 4))
                    {
                        return false;
                    }

                    length = MemoryMarshal.Read<uint>(binaryToken.Span.Slice(1));
                    break;

                default:
                    return false;
            }

            if (length > int.MaxValue)
            {
                return false;
            }

            binaryValue = binaryToken.Slice(1, (int)length);
            return true;
        }

        /// <summary>
        /// Gets the string value from the binary reader.
        /// </summary>
        /// <param name="stringToken">The buffer that has the string.</param>
        /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
        /// <returns>A string value from the binary reader.</returns>
        public static string GetStringValue(
            ReadOnlySpan<byte> stringToken,
            JsonStringDictionary jsonStringDictionary)
        {
            if (!JsonBinaryEncoding.TryGetStringValue(stringToken, jsonStringDictionary, out string result))
            {
                throw new JsonInvalidTokenException();
            }

            return result;
        }

        /// <summary>
        /// Try Get String Value
        /// </summary>
        /// <param name="stringToken">The buffer.</param>
        /// <param name="jsonStringDictionary">The dictionary to use for string decoding.</param>
        /// <param name="result">The result.</param>
        /// <returns>Whether we got the string.</returns>
        public static bool TryGetStringValue(
            ReadOnlySpan<byte> stringToken,
            JsonStringDictionary jsonStringDictionary,
            out string result)
        {
            result = null;
            if (stringToken.Length == 0)
            {
                return false;
            }

            if (JsonBinaryEncoding.TryGetEncodedStringValue(stringToken, jsonStringDictionary, out result))
            {
                return true;
            }

            if (JsonBinaryEncoding.TryGetUtf8String(stringToken, out result))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Try Get Encoded String Value
        /// </summary>
        /// <param name="stringToken">The string token to read from.</param>
        /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
        /// <param name="encodedStringValue">The encoded string if found.</param>
        /// <returns>Encoded String Value</returns>
        private static bool TryGetEncodedStringValue(
            ReadOnlySpan<byte> stringToken,
            JsonStringDictionary jsonStringDictionary,
            out string encodedStringValue)
        {
            encodedStringValue = default(string);

            bool found;
            if (JsonBinaryEncoding.TryGetEncodedSystemStringValue(stringToken, out encodedStringValue))
            {
                found = true;
            }
            else if (JsonBinaryEncoding.TryGetEncodedUserStringValue(stringToken, jsonStringDictionary, out encodedStringValue))
            {
                found = true;
            }
            else
            {
                found = false;
            }

            return found;
        }

        /// <summary>
        /// Try Get Encoded System String Value
        /// </summary>
        /// <param name="stringToken">The buffer to read from..</param>
        /// <param name="encodedSystemString">The encoded system string.</param>
        /// <returns>Encoded System String Value</returns>
        private static bool TryGetEncodedSystemStringValue(
            ReadOnlySpan<byte> stringToken,
            out string encodedSystemString)
        {
            encodedSystemString = default(string);
            if (stringToken.Length == 0)
            {
                return false;
            }

            int? systemStringId;
            if (stringToken.Length == 1 && JsonBinaryEncoding.TypeMarker.IsOneByteEncodedSystemString(stringToken[0]))
            {
                systemStringId = stringToken[0] - JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin;

            }
            else
            {
                systemStringId = null;
            }

            if (systemStringId.HasValue)
            {
                encodedSystemString = GetSystemStringById(systemStringId.Value);
            }

            return systemStringId.HasValue;
        }

        /// <summary>
        /// Try Get Encoded User String Value
        /// </summary>
        /// <param name="stringToken">The string token to read from.</param>
        /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
        /// <param name="encodedUserStringValue">The encoded user string value if found.</param>
        /// <returns>Whether or not the Encoded User String Value was found</returns>
        private static bool TryGetEncodedUserStringValue(
            ReadOnlySpan<byte> stringToken,
            JsonStringDictionary jsonStringDictionary,
            out string encodedUserStringValue)
        {
            encodedUserStringValue = default(string);
            if (jsonStringDictionary == null || stringToken.Length == 0)
            {
                return false;
            }

            int userStringId;
            if (stringToken.Length == 1 && JsonBinaryEncoding.TypeMarker.IsOneByteEncodedUserString(stringToken[0]))
            {
                userStringId = stringToken[0] - JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin;

            }
            else if (stringToken.Length == 2 && JsonBinaryEncoding.TypeMarker.IsTwoByteEncodedUserString(stringToken[0]))
            {
                const byte OneByteCount = JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMax - JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin;
                userStringId = OneByteCount
                    + stringToken[1]
                    + ((stringToken[0] - JsonBinaryEncoding.TypeMarker.UserString2ByteLengthMin) * 0xFF);
            }
            else
            {
                return false;
            }

            return jsonStringDictionary.TryGetStringAtIndex(userStringId, out encodedUserStringValue);
        }

        private static bool TryGetUtf8String(
            ReadOnlySpan<byte> stringToken,
            out string utf8StringValue)
        {
            utf8StringValue = null;
            if (stringToken.Length == 0)
            {
                return false;
            }

            byte typeMarker = stringToken[0];
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
                        if (stringToken.Length < (1 + 1))
                        {
                            return false;
                        }

                        length = MemoryMarshal.Read<byte>(stringToken.Slice(1));
                        break;

                    case JsonBinaryEncoding.TypeMarker.String2ByteLength:
                        if (stringToken.Length < (1 + 2))
                        {
                            return false;
                        }

                        length = MemoryMarshal.Read<ushort>(stringToken.Slice(1));
                        break;

                    case JsonBinaryEncoding.TypeMarker.String4ByteLength:
                        if (stringToken.Length < (1 + 4))
                        {
                            return false;
                        }

                        length = MemoryMarshal.Read<uint>(stringToken.Slice(1));
                        break;

                    default:
                        return false;
                }
            }

            if ((length > int.MaxValue) || length < 0)
            {
                return false;
            }

            unsafe
            {
                fixed (byte* spanPointer = stringToken)
                {
                    utf8StringValue = Encoding.UTF8.GetString(spanPointer, (int)length);
                }
            }

            return true;
        }

        public static JsonTokenType GetJsonTokenType(byte typeMarker)
        {
            JsonTokenType jsonTokenType;
            if (JsonBinaryEncoding.TypeMarker.IsEncodedNumberLiteral(typeMarker))
            {
                jsonTokenType = JsonTokenType.Number;
            }
            else if (JsonBinaryEncoding.TypeMarker.IsOneByteEncodedString(typeMarker))
            {
                jsonTokenType = JsonTokenType.String;
            }
            else if (JsonBinaryEncoding.TypeMarker.IsTwoByteEncodedString(typeMarker))
            {
                jsonTokenType = JsonTokenType.String;
            }
            else if (JsonBinaryEncoding.TypeMarker.IsEncodedLengthString(typeMarker))
            {
                jsonTokenType = JsonTokenType.String;
            }
            else
            {
                switch (typeMarker)
                {
                    // Single-byte values
                    case JsonBinaryEncoding.TypeMarker.Null:
                        jsonTokenType = JsonTokenType.Null;
                        break;

                    case JsonBinaryEncoding.TypeMarker.False:
                        jsonTokenType = JsonTokenType.False;
                        break;

                    case JsonBinaryEncoding.TypeMarker.True:
                        jsonTokenType = JsonTokenType.True;
                        break;

                    // Number values
                    case JsonBinaryEncoding.TypeMarker.NumberUInt8:
                    case JsonBinaryEncoding.TypeMarker.NumberInt16:
                    case JsonBinaryEncoding.TypeMarker.NumberInt32:
                    case JsonBinaryEncoding.TypeMarker.NumberInt64:
                    case JsonBinaryEncoding.TypeMarker.NumberDouble:
                        jsonTokenType = JsonTokenType.Number;
                        break;

                    // Extended Type System
                    case JsonBinaryEncoding.TypeMarker.Int8:
                        jsonTokenType = JsonTokenType.Int8;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Int16:
                        jsonTokenType = JsonTokenType.Int16;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Int32:
                        jsonTokenType = JsonTokenType.Int32;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Int64:
                        jsonTokenType = JsonTokenType.Int64;
                        break;

                    case JsonBinaryEncoding.TypeMarker.UInt32:
                        jsonTokenType = JsonTokenType.UInt32;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Float32:
                        jsonTokenType = JsonTokenType.Float32;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Float64:
                        jsonTokenType = JsonTokenType.Float64;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Guid:
                        jsonTokenType = JsonTokenType.Guid;
                        break;

                    case JsonBinaryEncoding.TypeMarker.Binary1ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Binary2ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Binary4ByteLength:
                        jsonTokenType = JsonTokenType.Binary;
                        break;

                    // Variable Length String Values
                    case JsonBinaryEncoding.TypeMarker.String1ByteLength:
                    case JsonBinaryEncoding.TypeMarker.String2ByteLength:
                    case JsonBinaryEncoding.TypeMarker.String4ByteLength:
                        jsonTokenType = JsonTokenType.String;
                        break;

                    // Array Values
                    case JsonBinaryEncoding.TypeMarker.EmptyArray:
                    case JsonBinaryEncoding.TypeMarker.SingleItemArray:
                    case JsonBinaryEncoding.TypeMarker.Array1ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Array1ByteLengthAndCount:
                    case JsonBinaryEncoding.TypeMarker.Array2ByteLengthAndCount:
                    case JsonBinaryEncoding.TypeMarker.Array4ByteLengthAndCount:
                        jsonTokenType = JsonTokenType.BeginArray;
                        break;

                    // Object Values
                    case JsonBinaryEncoding.TypeMarker.EmptyObject:
                    case JsonBinaryEncoding.TypeMarker.SinglePropertyObject:
                    case JsonBinaryEncoding.TypeMarker.Object1ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLength:
                    case JsonBinaryEncoding.TypeMarker.Object1ByteLengthAndCount:
                    case JsonBinaryEncoding.TypeMarker.Object2ByteLengthAndCount:
                    case JsonBinaryEncoding.TypeMarker.Object4ByteLengthAndCount:
                        jsonTokenType = JsonTokenType.BeginObject;
                        break;

                    default:
                        throw new JsonInvalidTokenException();
                }
            }

            return jsonTokenType;
        }

        public static bool TryGetValueLength(ReadOnlySpan<byte> buffer, out int length)
        {
            // Too lazy to convert this right now.
            length = (int)JsonBinaryEncoding.GetValueLength(buffer);
            return true;
        }

        /// <summary>
        /// Try Get Encoded String Type Marker
        /// </summary>
        /// <param name="value">the value</param>
        /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
        /// <param name="multiByteTypeMarker">The encoded string type marker if found.</param>
        /// <returns>Whether or not the type marker was found.</returns>
        public static bool TryGetEncodedStringTypeMarker(
            string value,
            JsonStringDictionary jsonStringDictionary,
            out MultiByteTypeMarker multiByteTypeMarker)
        {
            multiByteTypeMarker = default(MultiByteTypeMarker);
            if (value == null)
            {
                return false;
            }

            bool found;
            if (TryGetEncodedSystemStringTypeMarker(value, out multiByteTypeMarker))
            {
                found = true;
            }
            else if (TryGetEncodedUserStringTypeMarker(value, jsonStringDictionary, out multiByteTypeMarker))
            {
                found = true;
            }
            else
            {
                found = false;
            }

            return found;
        }

        /// <summary>
        /// Try Get Encoded System String Type Marker
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="multiByteTypeMarker">The multi byte type marker if found.</param>
        /// <returns>Whether or not the Encoded System String Type Marker was found.</returns>
        public static bool TryGetEncodedSystemStringTypeMarker(
            string value,
            out MultiByteTypeMarker multiByteTypeMarker)
        {
            multiByteTypeMarker = default(MultiByteTypeMarker);
            if (value == null)
            {
                return false;
            }

            if (TryGetSystemStringId(value, out int systemStringId))
            {
                multiByteTypeMarker = new MultiByteTypeMarker(
                    length: 1,
                    one: (byte)(TypeMarker.SystemString1ByteLengthMin + systemStringId));

                return true;
            }

            return false;
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
        /// <returns>
        /// - Positive Value: The length of the value including its TypeMarker
        /// - Negative Value: The length is encoded as an integer of size equals to abs(value) following the TypeMarker byte
        /// - Zero Value: The length is unknown (for instance an unassigned type marker)
        /// </returns>
        public static int GetValueLength(ReadOnlySpan<byte> buffer)
        {
            long valueLength = JsonBinaryEncoding.ValueLengths.GetValueLength(buffer);
            if (valueLength > int.MaxValue)
            {
                throw new InvalidOperationException($"{nameof(valueLength)} is greater than int.MaxValue");
            }

            return (int)valueLength;
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
        /// <param name="jsonStringDictionary">The optional json string dictionary.</param>
        /// <param name="multiByteTypeMarker">The multi byte type marker if found.</param>
        /// <returns>Whether or not the Encoded User String Type Marker was found.</returns>
        private static bool TryGetEncodedUserStringTypeMarker(
            string value,
            JsonStringDictionary jsonStringDictionary,
            out MultiByteTypeMarker multiByteTypeMarker)
        {
            multiByteTypeMarker = default(MultiByteTypeMarker);
            if (value == null)
            {
                return false;
            }

            const int MinStringLength = 2;
            const int MaxStringLength = 128;
            if (jsonStringDictionary != null && (value.Length >= MinStringLength) && (value.Length <= MaxStringLength))
            {
                const byte OneByteCount = TypeMarker.UserString1ByteLengthMax - TypeMarker.UserString1ByteLengthMin;
                if (jsonStringDictionary.TryAddString(value, out int index))
                {
                    // Convert the index to a multibyte type marker
                    if (index < OneByteCount)
                    {
                        multiByteTypeMarker = new MultiByteTypeMarker(
                            length: 1,
                            one: (byte)(TypeMarker.UserString1ByteLengthMin + index));
                    }
                    else
                    {
                        int twoByteOffset = index - OneByteCount;
                        multiByteTypeMarker = new MultiByteTypeMarker(
                            length: 2,
                            one: (byte)((twoByteOffset / 0xFF) + TypeMarker.UserString2ByteLengthMin),
                            two: (byte)(twoByteOffset % 0xFF));
                    }

                    return true;
                }
            }

            return false;
        }

        private static bool TryGetFixedWidthValue<T>(
            ReadOnlySpan<byte> token,
            int expectedTypeMarker,
            out T fixedWidthValue)
            where T : struct
        {
            fixedWidthValue = default(T);
            int sizeofType = Marshal.SizeOf(fixedWidthValue);
            if (token.Length < 1 + sizeofType)
            {
                return false;
            }

            byte typeMarker = token[0];
            if (typeMarker != expectedTypeMarker)
            {
                return false;
            }

            fixedWidthValue = MemoryMarshal.Read<T>(token.Slice(1));
            return true;
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

            #region [0x60, 0x80): 2-byte user string (32 values)
            /// <summary>
            /// The first type marker for a system string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte UserString2ByteLengthMin = UserString1ByteLengthMax;

            /// <summary>
            /// The last type marker for a system string whose value can be encoded in a 2 byte type marker.
            /// </summary>
            public const byte UserString2ByteLengthMax = UserString2ByteLengthMin + 32;
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

            #region [0xC0, 0xC8): Variable Length Strings and Binary Values
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
            public const byte Binary1ByteLength = 0xC3;

            /// <summary>
            /// Type marker for a Compressed string of 2-byte length
            /// </summary>
            public const byte Binary2ByteLength = 0xC4;

            /// <summary>
            /// Type marker for a Compressed string of 4-byte length
            /// </summary>
            public const byte Binary4ByteLength = 0xC5;

            // <empty> 0xC6
            // <empty> 0xC7
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

            /// <summary>
            /// The type marker for a GUID
            /// </summary>
            public const byte Guid = 0xD3;

            // <other types empty> 0xD4
            // <other types empty> 0xD5
            // <other types empty> 0xD6
            // <other types empty> 0xD7

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
            public static bool IsEncodedNumberLiteral(long value)
            {
                return InRange(value, LiteralIntMin, LiteralIntMax);
            }

            /// <summary>
            /// Gets whether an integer is a fixed length integer.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer is a fixed length integer.</returns>
            public static bool IsFixedLengthNumber(long value)
            {
                return InRange(value, NumberUInt8, NumberDouble + 1);
            }

            /// <summary>
            /// Gets whether an integer is a number.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>Whether an integer is a number.</returns>
            public static bool IsNumber(long value)
            {
                return IsEncodedNumberLiteral(value) || IsFixedLengthNumber(value);
            }

            /// <summary>
            /// Encodes an integer as a literal.
            /// </summary>
            /// <param name="value">The input integer.</param>
            /// <returns>The integer encoded as a literal if it can; else Invalid</returns>
            public static byte EncodeIntegerLiteral(long value)
            {
                return IsEncodedNumberLiteral(value) ? (byte)(LiteralIntMin + value) : Invalid;
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
            /// Gets whether a typeMarker is for a system string.
            /// </summary>
            /// <param name="typeMarker">The input type marker.</param>
            /// <returns>Whether the typeMarker is for a system string.</returns>
            public static bool IsSystemString(byte typeMarker)
            {
                return IsOneByteEncodedSystemString(typeMarker);
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
                return IsTwoByteEncodedUserString(typeMarker);
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
                return InRange(typeMarker, Binary1ByteLength, Binary4ByteLength + 1);
            }

            /// <summary>
            /// Gets whether a typeMarker is for a string.
            /// </summary>
            /// <param name="typeMarker">The type maker.</param>
            /// <returns>Whether the typeMarker is for a string.</returns>
            public static bool IsString(byte typeMarker)
            {
                return InRange(typeMarker, SystemString1ByteLengthMin, Binary4ByteLength + 1);
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

            public static bool IsGuid(byte typeMarker)
            {
                return typeMarker == Guid;
            }
            #endregion

            #region Array/Object Type Markers

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
            /// <summary>
            /// Initializes a new instance of the MultiByteTypeMarker struct.
            /// </summary>
            /// <param name="length">The length of the typemarker.</param>
            /// <param name="one">The first byte.</param>
            /// <param name="two">The second byte.</param>
            /// <param name="three">The third byte.</param>
            /// <param name="four">The fourth byte.</param>
            /// <param name="five">The fifth byte.</param>
            /// <param name="six">The sixth byte.</param>
            /// <param name="seven">The seventh byte.</param>
            public MultiByteTypeMarker(
                byte length,
                byte one = 0,
                byte two = 0,
                byte three = 0,
                byte four = 0,
                byte five = 0,
                byte six = 0,
                byte seven = 0)
            {
                this.Length = length;
                this.One = one;
                this.Two = two;
                this.Three = three;
                this.Four = four;
                this.Five = five;
                this.Six = six;
                this.Seven = seven;
            }

            public byte Length
            {
                get;
            }

            public byte One
            {
                get;
            }

            public byte Two
            {
                get;
            }

            public byte Three
            {
                get;
            }

            public byte Four
            {
                get;
            }

            public byte Five
            {
                get;
            }

            public byte Six
            {
                get;
            }

            public byte Seven
            {
                get;
            }
        }

        private static class ValueTypes
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
            private const JsonNodeType Number = JsonNodeType.Number;
            private const JsonNodeType Object = JsonNodeType.Object;
            private const JsonNodeType String = JsonNodeType.String;
            private const JsonNodeType True = JsonNodeType.True;
            private const JsonNodeType UInt32 = JsonNodeType.UInt32;
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

                // Encoded 2-byte user string (32 values)
                0, 0, 0, 0, 0, 0, 0, 0,
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

                // Variable Length String Values / Binary Values
                0,      // StrL1 (1-byte length)
                0,      // StrL2 (2-byte length)
                0,      // StrL4 (4-byte length)
                0,      // BinL1 (1-byte length)
                0,      // BinL2 (2-byte length)
                0,      // BinL4 (4-byte length)
                0,      // <empty> 0xC6
                0,      // <empty> 0xC7

                // Numeric Values
                0,      // NumUI8
                0,      // NumI16,
                0,      // NumI32,
                0,      // NumI64,
                0,      // NumDbl,
                0,      // Float32
                0,      // Float64
                0,      // <empty> 0xCF

                // Other Value Types
                0,      // Null
                0,      // False
                0,      // True
                0,      // Guid
                0,      // <empty> 0xD4
                0,      // <empty> 0xD5
                0,      // <empty> 0xD6
                0,      // <empty> 0xD7

                0,      // Int8
                0,      // Int16
                0,      // Int32
                0,      // Int64
                0,      // UInt32
                0,      // <empty> 0xDD
                0,      // <empty> 0xDE
                0,      // <empty> 0xDF

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
