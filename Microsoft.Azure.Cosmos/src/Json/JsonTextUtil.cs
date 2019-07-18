//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Globalization;
    using System.Text;

    /// <summary>
    /// Common utility class for JsonTextReader and JsonTextNavigator.
    /// Please treat this class as private.
    /// </summary>
    internal static class JsonTextUtil
    {
        /// <summary>
        /// Gets the number value from the specified token.
        /// </summary>
        /// <param name="bufferedToken">The jsonToken returned from that holds the raw number that you want the value of.</param>
        /// <returns>The number value from the specified token.</returns>
        public static double GetNumberValue(ArraySegment<byte> bufferedToken)
        {
            byte[] rawBufferedTokenArray = bufferedToken.Array;
            int offset = bufferedToken.Offset;
            int count = bufferedToken.Count;
            string stringDouble = Encoding.UTF8.GetString(rawBufferedTokenArray, offset, count);
            return double.Parse(stringDouble, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Gets the string value from the specified token.
        /// </summary>
        /// <param name="bufferedToken">The buffered token.</param>
        /// <returns>The string value from the specified token.</returns>
        public static string GetStringValue(ArraySegment<byte> bufferedToken)
        {
            // Offsetting by an additional character and removing 2 from the length since I want to skip the quotes.
            ArraySegment<byte> stringToken = new ArraySegment<byte>(bufferedToken.Array, bufferedToken.Offset + 1, bufferedToken.Count - 2);

            return JsonTextUtil.UnescapeJson(Encoding.UTF8.GetChars(stringToken.Array, stringToken.Offset, stringToken.Count));
        }

        /// <summary>
        /// Unescapes a json.
        /// </summary>
        /// <param name="escapedString">The escaped json.</param>
        /// <returns>The unescaped json.</returns>
        public static string UnescapeJson(char[] escapedString)
        {
            int readOffset = 0;
            int writeOffset = 0;

            while (readOffset != escapedString.Length)
            {
                if (escapedString[readOffset] == '\\')
                {
                    // Consume the '\' character
                    readOffset++;

                    // Figure out how to escape.
                    switch (escapedString[readOffset++])
                    {
                        case 'b':
                            escapedString[writeOffset++] = '\b';
                            break;
                        case 'f':
                            escapedString[writeOffset++] = '\f';
                            break;
                        case 'n':
                            escapedString[writeOffset++] = '\n';
                            break;
                        case 'r':
                            escapedString[writeOffset++] = '\r';
                            break;
                        case 't':
                            escapedString[writeOffset++] = '\t';
                            break;
                        case '\\':
                            escapedString[writeOffset++] = '\\';
                            break;
                        case '"':
                            escapedString[writeOffset++] = '"';
                            break;
                        case '/':
                            escapedString[writeOffset++] = '/';
                            break;
                        case 'u':
                            // parse Json unicode sequence: \uXXXX(\uXXXX)
                            // Start by reading XXXX. \u is already read.
                            char unescpaedUnicodeCharacter = (char)0;
                            for (int sequenceIndex = 0; sequenceIndex < 4; sequenceIndex++)
                            {
                                unescpaedUnicodeCharacter <<= 4;

                                char currentCharacter = escapedString[readOffset++];
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

                            escapedString[writeOffset++] = unescpaedUnicodeCharacter;
                            break;
                    }
                }
                else
                {
                    escapedString[writeOffset++] = escapedString[readOffset++];
                }
            }

            return new string(escapedString, 0, writeOffset);
        }
    }
}
