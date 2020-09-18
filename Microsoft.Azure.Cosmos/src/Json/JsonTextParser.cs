//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Buffers.Text;
    using System.Text;

    /// <summary>
    /// Common utility class for JsonTextReader and JsonTextNavigator.
    /// Please treat this class as private.
    /// </summary>
    internal static class JsonTextParser
    {
        private const int MaxStackAlloc = 1024;
        private static readonly ReadOnlyMemory<byte> ReverseSolidusBytes = new byte[] { (byte)'\\' };
        public static Number64 GetNumberValue(ReadOnlySpan<byte> token)
        {
            Number64 numberValue;
            if (Utf8Parser.TryParse(token, out long longValue, out int bytesConsumed1) && (bytesConsumed1 == token.Length))
            {
                numberValue = longValue;
            }
            else
            {
                if (!Utf8Parser.TryParse(token, out double doubleValue, out int bytesConsumed2) && (bytesConsumed2 == token.Length))
                {
                    throw new JsonNotNumberTokenException();
                }

                numberValue = doubleValue;
            }

            return numberValue;
        }

        public static string GetStringValue(ReadOnlySpan<byte> token)
        {
            // Offsetting by an additional character and removing 2 from the length since I want to skip the quotes.
            ReadOnlySpan<byte> stringToken = token.Slice(1, token.Length - 2);
            return JsonTextParser.UnescapeJson(stringToken);
        }

        public static sbyte GetInt8Value(ReadOnlySpan<byte> intToken)
        {
            const string prefix = "B";
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(
                prefix.Length,
                intToken.Length - prefix.Length);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > sbyte.MaxValue || value < sbyte.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(sbyte).FullName}");
            }

            return (sbyte)value;
        }

        public static short GetInt16Value(ReadOnlySpan<byte> intToken)
        {
            const string prefix = "H";
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(
                prefix.Length,
                intToken.Length - prefix.Length);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > short.MaxValue || value < short.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(short).FullName}");
            }

            return (short)value;
        }

        public static int GetInt32Value(ReadOnlySpan<byte> intToken)
        {
            const string prefix = "L";
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(
                prefix.Length,
                intToken.Length - prefix.Length);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > int.MaxValue || value < int.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(int).FullName}");
            }

            return (int)value;
        }

        public static long GetInt64Value(ReadOnlySpan<byte> intToken)
        {
            const string prefix = "LL";
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(
                prefix.Length,
                intToken.Length - prefix.Length);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > long.MaxValue || value < long.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(long).FullName}");
            }

            return value;
        }

        public static uint GetUInt32Value(ReadOnlySpan<byte> intToken)
        {
            const string prefix = "UL";
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(
                prefix.Length,
                intToken.Length - prefix.Length);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > uint.MaxValue || value < uint.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(uint).FullName}");
            }

            return (uint)value;
        }

        public static float GetFloat32Value(ReadOnlySpan<byte> floatToken)
        {
            const string prefix = "F";
            ReadOnlySpan<byte> floatTokenWithoutPrefix = floatToken.Slice(
                prefix.Length,
                floatToken.Length - prefix.Length);
            float value = JsonTextParser.GetFloatValue(floatTokenWithoutPrefix);
            if (value > float.MaxValue || value < float.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(float).FullName}");
            }

            return (float)value;
        }

        public static double GetFloat64Value(ReadOnlySpan<byte> floatToken)
        {
            const string prefix = "D";
            ReadOnlySpan<byte> floatTokenWithoutPrefix = floatToken.Slice(
                prefix.Length,
                floatToken.Length - prefix.Length);
            double value = JsonTextParser.GetDoubleValue(floatTokenWithoutPrefix);
            if (value > double.MaxValue || value < double.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(double).FullName}");
            }

            return (double)value;
        }

        public static Guid GetGuidValue(ReadOnlySpan<byte> guidToken)
        {
            const string prefix = "G";
            ReadOnlySpan<byte> guidTokenWithoutPrefix = guidToken.Slice(
                prefix.Length,
                guidToken.Length - prefix.Length);
            if (!Utf8Parser.TryParse(guidTokenWithoutPrefix, out Guid value, out _))
            {
                throw new JsonInvalidTokenException();
            }

            return value;
        }

        public static ReadOnlyMemory<byte> GetBinaryValue(ReadOnlySpan<byte> binaryToken)
        {
            const string prefix = "B";
            ReadOnlySpan<byte> binaryTokenWithoutPrefix = binaryToken.Slice(
                prefix.Length,
                binaryToken.Length - prefix.Length);
            string encodedString = Encoding.UTF8.GetString(binaryTokenWithoutPrefix.ToArray());
            return Convert.FromBase64String(encodedString);
        }

        private static double GetDoubleValue(ReadOnlySpan<byte> token)
        {
            if (!Utf8Parser.TryParse(token, out double value, out _))
            {
                throw new JsonNotNumberTokenException();
            }

            return value;
        }

        private static float GetFloatValue(ReadOnlySpan<byte> token)
        {
            if (!Utf8Parser.TryParse(token, out float value, out _))
            {
                throw new JsonNotNumberTokenException();
            }

            return value;
        }

        private static long GetIntegerValue(ReadOnlySpan<byte> token)
        {
            if (!Utf8Parser.TryParse(token, out long value, out _))
            {
                throw new JsonNotNumberTokenException();
            }

            return value;
        }

        /// <summary>
        /// Unescapes a json.
        /// </summary>
        /// <param name="escapedString">The escaped json.</param>
        /// <returns>The unescaped json.</returns>
        private static string UnescapeJson(ReadOnlySpan<byte> escapedString)
        {
            if (escapedString.IsEmpty)
            {
                return string.Empty;
            }

            if (escapedString.IndexOf(JsonTextParser.ReverseSolidusBytes.Span) < 0)
            {
                // String doesn't need escaping
                unsafe
                {
                    fixed (byte* escapedStringPointer = escapedString)
                    {
                        return Encoding.UTF8.GetString(escapedStringPointer, escapedString.Length);
                    }
                }
            }

            int readOffset = 0;
            int writeOffset = 0;

            int bufferLength;
            unsafe
            {
                fixed (byte* pointer = escapedString)
                {
                    bufferLength = Encoding.UTF8.GetCharCount(pointer, escapedString.Length);
                }
            }

            Span<char> stringBuffer = bufferLength <= MaxStackAlloc ? stackalloc char[bufferLength] : new char[bufferLength];
            unsafe
            {
                fixed (char* stringBufferPointer = stringBuffer)
                {
                    fixed (byte* escapedStringPointer = escapedString)
                    {
                        Encoding.UTF8.GetChars(escapedStringPointer, escapedString.Length, stringBufferPointer, bufferLength);
                    }
                }
            }

            while (readOffset != stringBuffer.Length)
            {
                if (stringBuffer[readOffset] == '\\')
                {
                    // Consume the '\' character
                    readOffset++;

                    // Figure out how to escape.
                    switch (stringBuffer[readOffset++])
                    {
                        case 'b':
                            stringBuffer[writeOffset++] = '\b';
                            break;
                        case 'f':
                            stringBuffer[writeOffset++] = '\f';
                            break;
                        case 'n':
                            stringBuffer[writeOffset++] = '\n';
                            break;
                        case 'r':
                            stringBuffer[writeOffset++] = '\r';
                            break;
                        case 't':
                            stringBuffer[writeOffset++] = '\t';
                            break;
                        case '\\':
                            stringBuffer[writeOffset++] = '\\';
                            break;
                        case '"':
                            stringBuffer[writeOffset++] = '"';
                            break;
                        case '/':
                            stringBuffer[writeOffset++] = '/';
                            break;
                        case 'u':
                            // parse Json unicode sequence: \uXXXX(\uXXXX)*
                            // Start by reading XXXX. \u is already read.
                            char unescpaedUnicodeCharacter = (char)0;
                            for (int sequenceIndex = 0; sequenceIndex < 4; sequenceIndex++)
                            {
                                unescpaedUnicodeCharacter <<= 4;

                                char currentCharacter = stringBuffer[readOffset++];
                                if (currentCharacter >= '0' && currentCharacter <= '9')
                                {
                                    unescpaedUnicodeCharacter += (char)(currentCharacter - '0');
                                }
                                else if (currentCharacter >= 'A' && currentCharacter <= 'F')
                                {
                                    unescpaedUnicodeCharacter += (char)(10 + currentCharacter - 'A');
                                }
                                else if (currentCharacter >= 'a' && currentCharacter <= 'f')
                                {
                                    unescpaedUnicodeCharacter += (char)(10 + currentCharacter - 'a');
                                }
                                else
                                {
                                    throw new JsonInvalidEscapedCharacterException();
                                }
                            }

                            stringBuffer[writeOffset++] = unescpaedUnicodeCharacter;
                            break;
                    }
                }
                else
                {
                    stringBuffer[writeOffset++] = stringBuffer[readOffset++];
                }
            }

            string value;
            unsafe
            {
                fixed (char* stringBufferPointer = stringBuffer)
                {
                    value = new string(stringBufferPointer, 0, writeOffset);
                }
            }

            return value;
        }
    }
}
