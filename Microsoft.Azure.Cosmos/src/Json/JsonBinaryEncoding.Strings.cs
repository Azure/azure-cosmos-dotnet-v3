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
            IReadOnlyJsonStringDictionary jsonStringDictionary)
        {
            if (stringToken.IsEmpty)
            {
                throw new JsonInvalidTokenException();
            }

            if (JsonBinaryEncoding.TryGetBufferedLengthPrefixedString(
                stringToken,
                out Utf8Memory lengthPrefixedString))
            {
                return lengthPrefixedString.ToString();
            }

            if (JsonBinaryEncoding.TryGetEncodedStringValue(
                stringToken.Span,
                jsonStringDictionary,
                out UtfAllString encodedStringValue))
            {
                return encodedStringValue.Utf16String;
            }

            throw new JsonInvalidTokenException();
        }

        public static bool TryGetBufferedStringValue(
            Utf8Memory stringToken,
            IReadOnlyJsonStringDictionary jsonStringDictionary,
            out Utf8Memory value)
        {
            if (stringToken.IsEmpty)
            {
                value = default;
                return false;
            }

            if (JsonBinaryEncoding.TryGetBufferedLengthPrefixedString(
                stringToken,
                out value))
            {
                return true;
            }

            if (JsonBinaryEncoding.TryGetEncodedStringValue(
                stringToken.Span,
                jsonStringDictionary,
                out UtfAllString encodedStringValue))
            {
                value = encodedStringValue.Utf8EscapedString;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Try Get Encoded String Value
        /// </summary>
        /// <param name="stringToken">The string token to read from.</param>
        /// <param name="jsonStringDictionary">The JSON string dictionary.</param>
        /// <param name="value">The encoded string if found.</param>
        /// <returns>Encoded String Value</returns>
        private static bool TryGetEncodedStringValue(
            Utf8Span stringToken,
            IReadOnlyJsonStringDictionary jsonStringDictionary,
            out UtfAllString value)
        {
            if (JsonBinaryEncoding.TryGetEncodedSystemStringValue(stringToken, out value))
            {
                return true;
            }

            if (JsonBinaryEncoding.TryGetEncodedUserStringValue(stringToken, jsonStringDictionary, out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Try Get Encoded System String Value
        /// </summary>
        /// <param name="stringToken">The buffer to read from..</param>
        /// <param name="value">The encoded system string.</param>
        /// <returns>Encoded System String Value</returns>
        private static bool TryGetEncodedSystemStringValue(
            Utf8Span stringToken,
            out UtfAllString value)
        {
            if (!JsonBinaryEncoding.TypeMarker.IsSystemString(stringToken.Span[0]))
            {
                value = default;
                return false;
            }

            if (stringToken.Length < 1)
            {
                value = default;
                return false;
            }

            int systemStringId = stringToken.Span[0] - JsonBinaryEncoding.TypeMarker.SystemString1ByteLengthMin;
            return JsonBinaryEncoding.TryGetSystemStringById(systemStringId, out value);
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
            out Utf8Memory value)
        {
            ReadOnlySpan<byte> stringTokenSpan = stringToken.Memory.Span;
            byte typeMarker = stringTokenSpan[0];
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
                            value = default;
                            return false;
                        }

                        start = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.OneByteLength;
                        length = stringTokenSpan[0];
                        break;

                    case JsonBinaryEncoding.TypeMarker.String2ByteLength:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.TwoByteLength)
                        {
                            value = default;
                            return false;
                        }

                        start = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.TwoByteLength;
                        length = MemoryMarshal.Read<ushort>(stringTokenSpan);
                        break;

                    case JsonBinaryEncoding.TypeMarker.String4ByteLength:
                        if (stringTokenSpan.Length < JsonBinaryEncoding.FourByteLength)
                        {
                            value = default;
                            return false;
                        }

                        start = JsonBinaryEncoding.TypeMarkerLength + JsonBinaryEncoding.FourByteLength;
                        length = MemoryMarshal.Read<uint>(stringTokenSpan);
                        break;

                    default:
                        value = default;
                        return false;
                }

                if ((start + length) > stringToken.Length)
                {
                    value = default;
                    return false;
                }
            }

            value = stringToken.Slice(start: start, length: (int)length);
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
