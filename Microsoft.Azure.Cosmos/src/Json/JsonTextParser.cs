//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Buffers.Text;
    using System.Text;
    using Microsoft.Azure.Cosmos.Core.Utf8;

    /// <summary>
    /// Common utility class for JsonTextReader and JsonTextNavigator.
    /// Please treat this class as private.
    /// </summary>
    internal static class JsonTextParser
    {
        private static readonly ReadOnlyMemory<byte> ReverseSolidusBytes = new byte[] { (byte)'\\' };

        private static class Utf16Surrogate
        {
            public static class High
            {
                public const char Min = (char)0xD800;
                public const char Max = (char)0xDBFF;
            }

            public static class Low
            {
                public const char Min = (char)0xDC00;
                public const char Max = (char)0xDFFF;
            }
        }

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

        public static bool TryGetUInt64Value(ReadOnlySpan<byte> token, out ulong value)
        {
            return Utf8Parser.TryParse(token, out value, out int bytesConsumed1) && (bytesConsumed1 == token.Length);
        }

        public static Utf8String GetStringValue(Utf8Memory token)
        {
            // Offsetting by an additional character and removing 2 from the length since we want to skip the quotes.
            Utf8Memory stringToken = token.Slice(1, token.Length - 2);
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

        private static Utf8String UnescapeJson(Utf8Memory escapedString, bool checkIfNeedsEscaping = true)
        {
            if (escapedString.IsEmpty)
            {
                return Utf8String.Empty;
            }

            if (checkIfNeedsEscaping && (escapedString.Span.Span.IndexOf(JsonTextParser.ReverseSolidusBytes.Span) < 0))
            {
                // String doesn't need unescaping
                return Utf8String.UnsafeFromUtf8BytesNoValidation(escapedString.Memory);
            }

            Memory<byte> stringBuffer = new byte[escapedString.Length];
            escapedString.Memory.CopyTo(stringBuffer);

            Span<byte> stringBufferSpan = stringBuffer.Span;

            int readOffset = 0;
            int writeOffset = 0;
            while (readOffset != stringBuffer.Length)
            {
                if (stringBufferSpan[readOffset] == '\\')
                {
                    // Consume the '\' character
                    readOffset++;

                    // Figure out how to escape.
                    switch (stringBufferSpan[readOffset++])
                    {
                        case (byte)'b':
                            stringBufferSpan[writeOffset++] = (byte)'\b';
                            break;
                        case (byte)'f':
                            stringBufferSpan[writeOffset++] = (byte)'\f';
                            break;
                        case (byte)'n':
                            stringBufferSpan[writeOffset++] = (byte)'\n';
                            break;
                        case (byte)'r':
                            stringBufferSpan[writeOffset++] = (byte)'\r';
                            break;
                        case (byte)'t':
                            stringBufferSpan[writeOffset++] = (byte)'\t';
                            break;
                        case (byte)'\\':
                            stringBufferSpan[writeOffset++] = (byte)'\\';
                            break;
                        case (byte)'"':
                            stringBufferSpan[writeOffset++] = (byte)'"';
                            break;
                        case (byte)'/':
                            stringBufferSpan[writeOffset++] = (byte)'/';
                            break;
                        case (byte)'u':
                            // parse JSON unicode code point: \uXXXX(\uYYYY)
                            // Start by reading XXXX, since \u is already read.
                            char escapeSequence = (char)0;
                            for (int escapeSequenceIndex = 0; escapeSequenceIndex < 4; escapeSequenceIndex++)
                            {
                                escapeSequence <<= 4;

                                byte currentCharacter = stringBufferSpan[readOffset++];
                                if (currentCharacter >= '0' && currentCharacter <= '9')
                                {
                                    escapeSequence += (char)(currentCharacter - '0');
                                }
                                else if (currentCharacter >= 'A' && currentCharacter <= 'F')
                                {
                                    escapeSequence += (char)(10 + currentCharacter - 'A');
                                }
                                else if (currentCharacter >= 'a' && currentCharacter <= 'f')
                                {
                                    escapeSequence += (char)(10 + currentCharacter - 'a');
                                }
                                else
                                {
                                    throw new JsonInvalidEscapedCharacterException();
                                }
                            }

                            if ((escapeSequence >= Utf16Surrogate.High.Min) && (escapeSequence <= Utf16Surrogate.High.Max))
                            {
                                // We have a high surrogate + low surrogate pair
                                if (stringBufferSpan[readOffset++] != '\\')
                                {
                                    throw new JsonInvalidEscapedCharacterException();
                                }

                                if (stringBufferSpan[readOffset++] != 'u')
                                {
                                    throw new JsonInvalidEscapedCharacterException();
                                }

                                char highSurrogate = escapeSequence;

                                char lowSurrogate = (char)0;
                                for (int escapeSequenceIndex = 0; escapeSequenceIndex < 4; escapeSequenceIndex++)
                                {
                                    lowSurrogate <<= 4;

                                    byte currentCharacter = stringBufferSpan[readOffset++];
                                    if (currentCharacter >= '0' && currentCharacter <= '9')
                                    {
                                        lowSurrogate += (char)(currentCharacter - '0');
                                    }
                                    else if (currentCharacter >= 'A' && currentCharacter <= 'F')
                                    {
                                        lowSurrogate += (char)(10 + currentCharacter - 'A');
                                    }
                                    else if (currentCharacter >= 'a' && currentCharacter <= 'f')
                                    {
                                        lowSurrogate += (char)(10 + currentCharacter - 'a');
                                    }
                                    else
                                    {
                                        throw new JsonInvalidEscapedCharacterException();
                                    }
                                }

                                writeOffset += WideCharToMultiByte(highSurrogate, lowSurrogate, stringBufferSpan.Slice(start: writeOffset));
                            }
                            else
                            {
                                writeOffset += WideCharToMultiByte(escapeSequence, stringBufferSpan.Slice(start: writeOffset));
                            }

                            break;
                    }
                }
                else
                {
                    stringBufferSpan[writeOffset++] = stringBufferSpan[readOffset++];
                }
            }

            return Utf8String.UnsafeFromUtf8BytesNoValidation(stringBuffer.Slice(start: 0, writeOffset));
        }

        private static int WideCharToMultiByte(char value, Span<byte> multiByteBuffer)
        {
            Span<char> charArray = stackalloc char[1];
            charArray[0] = value;

            return Encoding.UTF8.GetBytes(charArray, multiByteBuffer);
        }

        private static int WideCharToMultiByte(char highSurrogate, char lowSurrogate, Span<byte> multiByteBuffer)
        {
            Span<char> charArray = stackalloc char[2];
            charArray[0] = highSurrogate;
            charArray[1] = lowSurrogate;

            return Encoding.UTF8.GetBytes(charArray, multiByteBuffer);
        }
    }
}
