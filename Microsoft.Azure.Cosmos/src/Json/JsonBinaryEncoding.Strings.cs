// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.Azure.Cosmos.Core.Utf8;

    internal static partial class JsonBinaryEncoding
    {
        /// <summary>
        /// Gets the string value from the binary reader.
        /// </summary>
        /// <param name="stringToken">The buffer that has the string.</param>
        /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
        /// <returns>A string value from the binary reader.</returns>
        public static string GetStringValue(
            Utf8Memory stringToken,
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
            Utf8Memory stringToken,
            IReadOnlyJsonStringDictionary jsonStringDictionary,
            out string result)
        {
            if (stringToken.IsEmpty)
            {
                result = default;
                return false;
            }

            if (JsonBinaryEncoding.TryGetBufferedStringValue(stringToken, jsonStringDictionary, out Utf8Memory bufferedUtf8StringValue))
            {
                result = bufferedUtf8StringValue.ToString();
                return true;
            }

            result = default;
            return false;
        }

        public static bool TryGetBufferedStringValue(
            Utf8Memory stringToken,
            IReadOnlyJsonStringDictionary jsonStringDictionary,
            out Utf8Memory bufferedUtf8StringValue)
        {
            if (stringToken.IsEmpty)
            {
                bufferedUtf8StringValue = default;
                return false;
            }

            if (JsonBinaryEncoding.TryGetBufferedLengthPrefixedString(stringToken, out bufferedUtf8StringValue))
            {
                return true;
            }

            if (JsonBinaryEncoding.TryGetEncodedStringValue(stringToken.Span, jsonStringDictionary, out UtfAllString encodedStringValue))
            {
                bufferedUtf8StringValue = encodedStringValue.Utf8String;
                return true;
            }

            bufferedUtf8StringValue = default;
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
            Utf8Span stringToken,
            IReadOnlyJsonStringDictionary jsonStringDictionary,
            out UtfAllString encodedStringValue)
        {
            if (JsonBinaryEncoding.TryGetEncodedSystemStringValue(stringToken, out encodedStringValue))
            {
                return true;
            }

            if (JsonBinaryEncoding.TryGetEncodedUserStringValue(stringToken, jsonStringDictionary, out encodedStringValue))
            {
                return true;
            }

            encodedStringValue = default;
            return false;
        }

        /// <summary>
        /// Try Get Encoded System String Value
        /// </summary>
        /// <param name="stringToken">The buffer to read from..</param>
        /// <param name="encodedSystemString">The encoded system string.</param>
        /// <returns>Encoded System String Value</returns>
        private static bool TryGetEncodedSystemStringValue(
            Utf8Span stringToken,
            out UtfAllString encodedSystemString)
        {
            if (stringToken.IsEmpty)
            {
                encodedSystemString = default;
                return false;
            }

            if (!JsonBinaryEncoding.TypeMarker.IsOneByteEncodedSystemString(stringToken.Span[0]))
            {
                encodedSystemString = default;
                return false;
            }

            if (stringToken.Length < 1)
            {
                encodedSystemString = default;
                return false;
            }

            int systemStringId = stringToken.Span[0] - JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin;
            return JsonBinaryEncoding.TryGetSystemStringById(systemStringId, out encodedSystemString);
        }

        /// <summary>
        /// Try Get Encoded User String Value
        /// </summary>
        /// <param name="stringToken">The string token to read from.</param>
        /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
        /// <param name="encodedUserStringValue">The encoded user string value if found.</param>
        /// <returns>Whether or not the Encoded User String Value was found</returns>
        private static bool TryGetEncodedUserStringValue(
            Utf8Span stringToken,
            IReadOnlyJsonStringDictionary jsonStringDictionary,
            out UtfAllString encodedUserStringValue)
        {
            if (jsonStringDictionary == null)
            {
                encodedUserStringValue = default;
                return false;
            }

            if (!JsonBinaryEncoding.TryGetUserStringId(stringToken, out int userStringId))
            {
                encodedUserStringValue = default;
                return false;
            }

            return jsonStringDictionary.TryGetStringAtIndex(userStringId, out encodedUserStringValue);
        }

        private static bool TryGetUserStringId(Utf8Span stringToken, out int userStringId)
        {
            if (stringToken.IsEmpty)
            {
                userStringId = default;
                return false;
            }

            byte typeMarker = stringToken.Span[0];
            if (!JsonBinaryEncoding.TypeMarker.IsUserString(typeMarker))
            {
                userStringId = default;
                return false;
            }

            if (JsonBinaryEncoding.TypeMarker.IsOneByteEncodedUserString(typeMarker))
            {
                if (stringToken.Length < 1)
                {
                    userStringId = default;
                    return false;
                }

                userStringId = stringToken.Span[0] - JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin;
            }
            else //// JsonBinaryEncoding.TypeMarker.IsTwoByteEncodedUserString(typeMarker)
            {
                if (stringToken.Length < 2)
                {
                    userStringId = default;
                    return false;
                }

                const byte OneByteCount = JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMax - JsonBinaryEncoding.TypeMarker.UserString1ByteLengthMin;
                userStringId = OneByteCount
                    + stringToken.Span[1]
                    + ((stringToken.Span[0] - JsonBinaryEncoding.TypeMarker.UserString2ByteLengthMin) * 0xFF);
            }

            return true;
        }

        private static bool TryGetBufferedLengthPrefixedString(
            Utf8Memory stringToken,
            out Utf8Memory utf8String)
        {
            if (stringToken.IsEmpty)
            {
                utf8String = default;
                return false;
            }

            ReadOnlySpan<byte> stringTokenSpan = stringToken.Span.Span;
            byte typeMarker = stringToken.Span.Span[0];
            stringTokenSpan = stringTokenSpan.Slice(start: 1);

            int start;
            long length;
            if (JsonBinaryEncoding.TypeMarker.IsEncodedLengthString(typeMarker))
            {
                start = JsonBinaryEncoding.TypeMarkerLength;
                length = JsonBinaryEncoding.GetStringLengths(typeMarker);
            }
            else
            {
                switch (typeMarker)
                {
                    case JsonBinaryEncoding.TypeMarker.String1ByteLength:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.OneByteLength)
                        {
                            utf8String = default;
                            return false;
                        }

                        start = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength;
                        length = MemoryMarshal.Read<byte>(stringTokenSpan);
                        break;

                    case JsonBinaryEncoding.TypeMarker.String2ByteLength:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.TwoByteLength)
                        {
                            utf8String = default;
                            return false;
                        }

                        start = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.TwoByteLength;
                        length = MemoryMarshal.Read<ushort>(stringTokenSpan);
                        break;

                    case JsonBinaryEncoding.TypeMarker.String4ByteLength:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.FourByteLength)
                        {
                            utf8String = default;
                            return false;
                        }

                        start = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength;
                        length = MemoryMarshal.Read<uint>(stringTokenSpan);
                        break;

                    default:
                        utf8String = default;
                        return false;
                }

                if ((start + length) > stringToken.Length)
                {
                    utf8String = default;
                    return false;
                }
            }

            utf8String = stringToken.Slice(start: start, length: (int)length);
            return true;
        }

        /// <summary>
        /// Try Get Encoded String Type Marker
        /// </summary>
        /// <param name="utf8Span">the value</param>
        /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
        /// <param name="multiByteTypeMarker">The encoded string type marker if found.</param>
        /// <returns>Whether or not the type marker was found.</returns>
        public static bool TryGetEncodedStringTypeMarker(
            Utf8Span utf8Span,
            JsonStringDictionary jsonStringDictionary,
            out MultiByteTypeMarker multiByteTypeMarker)
        {
            if (JsonBinaryEncoding.TryGetEncodedSystemStringTypeMarker(utf8Span, out multiByteTypeMarker))
            {
                return true;
            }

            if (JsonBinaryEncoding.TryGetEncodedUserStringTypeMarker(utf8Span, jsonStringDictionary, out multiByteTypeMarker))
            {
                return true;
            }

            multiByteTypeMarker = default;
            return false;
        }

        /// <summary>
        /// Try Get Encoded System String Type Marker
        /// </summary>
        /// <param name="utf8Span">The value.</param>
        /// <param name="multiByteTypeMarker">The multi byte type marker if found.</param>
        /// <returns>Whether or not the Encoded System String Type Marker was found.</returns>
        private static bool TryGetEncodedSystemStringTypeMarker(
            Utf8Span utf8Span,
            out MultiByteTypeMarker multiByteTypeMarker)
        {
            if (JsonBinaryEncoding.TryGetSystemStringId(utf8Span, out int systemStringId))
            {
                multiByteTypeMarker = new MultiByteTypeMarker(
                    length: 1,
                    one: (byte)(TypeMarker.SystemString1ByteLengthMin + systemStringId));

                return true;
            }

            multiByteTypeMarker = default;
            return false;
        }

        /// <summary>
        /// Try Get Encoded User String Type Marker
        /// </summary>
        /// <param name="utf8Span">The value.</param>
        /// <param name="jsonStringDictionary">The optional json string dictionary.</param>
        /// <param name="multiByteTypeMarker">The multi byte type marker if found.</param>
        /// <returns>Whether or not the Encoded User String Type Marker was found.</returns>
        private static bool TryGetEncodedUserStringTypeMarker(
            Utf8Span utf8Span,
            JsonStringDictionary jsonStringDictionary,
            out MultiByteTypeMarker multiByteTypeMarker)
        {
            if (jsonStringDictionary == null)
            {
                multiByteTypeMarker = default;
                return false;
            }

            const int MinStringLength = 2;
            const int MaxStringLength = 128;
            if ((utf8Span.Length < MinStringLength) || (utf8Span.Length > MaxStringLength))
            {
                multiByteTypeMarker = default;
                return false;
            }

            const byte OneByteCount = TypeMarker.UserString1ByteLengthMax - TypeMarker.UserString1ByteLengthMin;
            if (!jsonStringDictionary.TryAddString(utf8Span, out int index))
            {
                multiByteTypeMarker = default;
                return false;
            }

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
}
