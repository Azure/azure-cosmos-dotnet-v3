//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Buffers.Text;
    using System.Runtime.CompilerServices;
    using System.Text;

    /// <summary>
    /// Common utility class for JsonTextReader and JsonTextNavigator.
    /// Please treat this class as private.
    /// </summary>
    internal static class JsonTextParser
    {
        public static Number64 GetNumberValue(ReadOnlySpan<byte> token)
        {
            Number64 numberValue;
            if (Utf8Parser.TryParse(token, out long longValue, out int bytesConsumed1))
            {
                numberValue = longValue;
            }
            else
            {
                if (!Utf8Parser.TryParse(token, out double doubleValue, out int bytesConsumed2))
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
            ReadOnlySpan<byte> stringToken = token.Slice(1, token.Length);
            return JsonTextParser.UnescapeJson(token);
        }

        public static sbyte GetInt8Value(ReadOnlySpan<byte> intToken)
        {
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(1, intToken.Length - 1);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > sbyte.MaxValue || value < sbyte.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(sbyte).FullName}");
            }

            return (sbyte)value;
        }

        public static short GetInt16Value(ReadOnlySpan<byte> intToken)
        {
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(1, intToken.Length - 1);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > short.MaxValue || value < short.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(short).FullName}");
            }

            return (short)value;
        }

        public static int GetInt32Value(ReadOnlySpan<byte> intToken)
        {
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(1, intToken.Length - 1);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > int.MaxValue || value < int.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(int).FullName}");
            }

            return (int)value;
        }

        public static long GetInt64Value(ReadOnlySpan<byte> intToken)
        {
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(2, intToken.Length - 2);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > long.MaxValue || value < long.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(long).FullName}");
            }

            return (long)value;
        }

        public static uint GetUInt32Value(ReadOnlySpan<byte> intToken)
        {
            ReadOnlySpan<byte> intTokenWithoutPrefix = intToken.Slice(2, intToken.Length - 2);
            long value = JsonTextParser.GetIntegerValue(intTokenWithoutPrefix);
            if (value > uint.MaxValue || value < uint.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(uint).FullName}");
            }

            return (uint)value;
        }

        public static float GetFloat32Value(ReadOnlySpan<byte> floatToken)
        {
            ReadOnlySpan<byte> floatTokenWithoutPrefix = floatToken.Slice(1, floatToken.Length - 1);
            float value = JsonTextParser.GetFloatValue(floatTokenWithoutPrefix);
            if (value > float.MaxValue || value < float.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(float).FullName}");
            }

            return (float)value;
        }

        public static double GetFloat64Value(ReadOnlySpan<byte> floatToken)
        {
            ReadOnlySpan<byte> floatTokenWithoutPrefix = floatToken.Slice(1, floatToken.Length - 1);
            double value = JsonTextParser.GetDoubleValue(floatTokenWithoutPrefix);
            if (value > double.MaxValue || value < double.MinValue)
            {
                throw new ArgumentOutOfRangeException($"Tried to read {value} as an {typeof(double).FullName}");
            }

            return (double)value;
        }

        public static Guid GetGuidValue(ReadOnlySpan<byte> guidToken)
        {
            ReadOnlySpan<byte> guidTokenWithoutPrefix = guidToken.Slice(1, guidToken.Length - 1);
            if (!Utf8Parser.TryParse(guidToken, out Guid value, out int bytesConsumed))
            {
                throw new JsonInvalidTokenException();
            }

            return value;
        }

        public static ReadOnlySpan<byte> GetBinaryValue(ReadOnlySpan<byte> binaryToken)
        {
            ReadOnlySpan<byte> binaryTokenWithoutPrefix = binaryToken.Slice(1, binaryToken.Length - 1);
            string encodedString = Encoding.UTF8.GetString(binaryTokenWithoutPrefix.ToArray());
            return Convert.FromBase64String(encodedString);
        }

        private static double GetDoubleValue(ReadOnlySpan<byte> token)
        {
            if (!Utf8Parser.TryParse(token, out double value, out int bytesConsumed))
            {
                throw new JsonNotNumberTokenException();
            }

            return value;
        }

        private static float GetFloatValue(ReadOnlySpan<byte> token)
        {
            if (!Utf8Parser.TryParse(token, out float value, out int bytesConsumed))
            {
                throw new JsonNotNumberTokenException();
            }

            return value;
        }

        private static long GetIntegerValue(ReadOnlySpan<byte> token)
        {
            if (!Utf8Parser.TryParse(token, out long value, out int bytesConsumed))
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
            int readOffset = 0;
            int writeOffset = 0;

            Span<byte> stringBuffer = escapedString.ToArray();

            while (readOffset != escapedString.Length)
            {
                if (stringBuffer[readOffset] == '\\')
                {
                    // Consume the '\' character
                    readOffset++;

                    // Figure out how to escape.
                    switch (stringBuffer[readOffset++])
                    {
                        case (byte)'b':
                            stringBuffer[writeOffset++] = (byte)'\b';
                            break;
                        case (byte)'f':
                            stringBuffer[writeOffset++] = (byte)'\f';
                            break;
                        case (byte)'n':
                            stringBuffer[writeOffset++] = (byte)'\n';
                            break;
                        case (byte)'r':
                            stringBuffer[writeOffset++] = (byte)'\r';
                            break;
                        case (byte)'t':
                            stringBuffer[writeOffset++] = (byte)'\t';
                            break;
                        case (byte)'\\':
                            stringBuffer[writeOffset++] = (byte)'\\';
                            break;
                        case (byte)'"':
                            stringBuffer[writeOffset++] = (byte)'"';
                            break;
                        case (byte)'/':
                            stringBuffer[writeOffset++] = (byte)'/';
                            break;
                        case (byte)'u':
                            // parse Json unicode sequence: \uXXXX(\uXXXX)
                            // Start by reading XXXX. \u is already read.
                            byte unescpaedUnicodeCharacter = 0;
                            for (int sequenceIndex = 0; sequenceIndex < 4; sequenceIndex++)
                            {
                                unescpaedUnicodeCharacter <<= 4;

                                byte currentCharacter = stringBuffer[readOffset++];
                                if (currentCharacter >= '0' && currentCharacter <= '9')
                                {
                                    unescpaedUnicodeCharacter += (byte)(currentCharacter - '0');
                                }
                                else if (currentCharacter >= 'A' && currentCharacter <= 'F')
                                {
                                    unescpaedUnicodeCharacter += (byte)(10 + currentCharacter - 'A');
                                }
                                else if (currentCharacter >= 'a' && currentCharacter <= 'f')
                                {
                                    unescpaedUnicodeCharacter += (byte)(10 + currentCharacter - 'a');
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

            unsafe
            {
                return Encoding.UTF8.GetString(
                    (byte*)Unsafe.AsPointer(ref stringBuffer.GetPinnableReference()),
                    stringBuffer.Length);
            }
        }
    }
}
