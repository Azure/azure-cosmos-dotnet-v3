//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Mainly copied from https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Convert.Base64.cs
    /// Implementation does not support spaces inside the message given is is not a case for ResourceIds.
    /// Do not modify this file. Take any changes necessary outside of this code that will be eventually replaced with the runtime implementation.
    /// </summary>
#if NETSTANDARD2_1 || NETCOREAPP
#error Switch to https://docs.microsoft.com/en-us/dotnet/api/system.convert.tryfrombase64string?view=net-6.0
#endif
    internal static class ResourceIdBase64Decoder
    {
        public unsafe static bool TryDecode(string base64string, out byte[] bytes)
        {
            bytes = null;
            if (string.IsNullOrEmpty(base64string))
            {
                return false;
            }

            fixed (char* srcChars = base64string) {
                int srcLength = base64string.Length;
                    
                // We need to get rid of any trailing white spaces.
                // Otherwise we would be rejecting input such as "abc= ":
                while (srcLength > 0)
                {
                    int lastChar = srcChars[srcLength - 1];
                    if (lastChar != Space)
                    {
                        break;
                    }
                    srcLength--;
                }

                if (TryComputeResultLength(srcChars, srcLength, out int destLength) == false)
                {
                    return false;
                }
                bytes = new byte[destLength];

                int sourceIndex = 0;
                int destIndex = 0;

                // Last bytes could have padding characters, so process them separately and treat them as valid.
                int maxSrcLength = srcLength - 4;

                while (sourceIndex < maxSrcLength)
                {
                    int result = Decode(srcChars, sourceIndex);
                    if (result < 0)
                    {
                        bytes = default;
                        return false;
                    }
                    WriteThreeLowOrderBytes(bytes, destIndex, result);
                    destIndex += 3;
                    sourceIndex += 4;
                }

                int i0 = srcChars[srcLength - 4];
                int i1 = srcChars[srcLength - 3];
                int i2 = srcChars[srcLength - 2];
                int i3 = srcChars[srcLength - 1];
                if (((i0 | i1 | i2 | i3) & 0xffffff00) != 0)
                {
                    bytes = default;
                    return false;
                }

                i0 = DecodingMap[i0];
                i1 = DecodingMap[i1];

                i0 <<= 18;
                i1 <<= 12;

                i0 |= i1;

                if (i3 != EncodingPad)
                {
                    i2 = DecodingMap[i2];
                    i3 = DecodingMap[i3];

                    i2 <<= 6;

                    i0 |= i3;
                    i0 |= i2;

                    if (i0 < 0)
                    {
                        bytes = default;
                        return false;
                    }

                    if (destIndex > destLength - 3)
                    {
                        bytes = default;
                        return false;
                    }

                    WriteThreeLowOrderBytes(bytes, destIndex, i0);
                    destIndex += 3;
                }
                else if (i2 != EncodingPad)
                {
                    i2 = DecodingMap[i2];

                    i2 <<= 6;

                    i0 |= i2;

                    if (i0 < 0)
                    {
                        bytes = default;
                        return false;
                    }

                    if (destIndex > destLength - 2)
                    {
                        bytes = default;
                        return false;
                    }

                    bytes[destIndex] = (byte)(i0 >> 16);
                    bytes[destIndex + 1] = (byte)(i0 >> 8);
                    destIndex += 2;
                }
                else
                {
                    if (i0 < 0)
                    {
                        bytes = default;
                        return false;
                    }

                    if (destIndex > destLength - 1)
                    {
                        bytes = default;
                        return false;
                    }

                    bytes[destIndex] = (byte)(i0 >> 16);
                    destIndex++;
                }

                return true;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static int Decode(char* encodedChars, int sourceIndex)
        {
            int i0 = encodedChars[sourceIndex];
            int i1 = encodedChars[sourceIndex + 1];
            int i2 = encodedChars[sourceIndex + 2];
            int i3 = encodedChars[sourceIndex + 3];

            if (((i0 | i1 | i2 | i3) & 0xffffff00) != 0)
            {
                return -1; // One or more chars falls outside the 00..ff range. This cannot be a valid Base64 character.
            }

            i0 = DecodingMap[i0];
            i1 = DecodingMap[i1];
            i2 = DecodingMap[i2];
            i3 = DecodingMap[i3];

            i0 <<= 18;
            i1 <<= 12;
            i2 <<= 6;

            i0 |= i3;
            i1 |= i2;

            i0 |= i1;
            return i0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteThreeLowOrderBytes(byte[] destination, int destIndex, int value)
        {
            destination[destIndex] = (byte)(value >> 16);
            destination[destIndex + 1] = (byte)(value >> 8);
            destination[destIndex + 2] = (byte)value;
        }

        /// <summary>
        /// Compute the number of bytes encoded in the specified Base 64 char array:
        /// Walk the entire input counting white spaces and padding chars, then compute result length
        /// based on 3 bytes per 4 chars.
        /// </summary>
        private static unsafe bool TryComputeResultLength(char* inputPtr, int inputLength, out int resultLength)
        {
            resultLength = 0;

            if (inputLength >= 3 && inputPtr[inputLength - 3] == EncodingPad)
            {
                return false;
            }
            else if (inputLength >= 2 && inputPtr[inputLength - 2] == EncodingPad)
            {
                // Two trailing pads
                resultLength = ((inputLength - 2) >> 2) * 3 + 1;
            }
            else if (inputPtr[inputLength - 1] == EncodingPad)
            {
                // One trailing pad
                resultLength = ((inputLength - 1) >> 2) * 3 + 2;
            }
            else
            {
                resultLength = (inputLength >> 2) * 3;
            }

            return true;
        }

        private const byte EncodingPad = (byte)'=';
        private const byte Space = (byte)' ';

        // Pre-computing this table using a custom string(s_characters) and GenerateDecodingMapAndVerify (found in tests)
        private readonly static sbyte[] DecodingMap = new sbyte[]
        {
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, 62, -1, -1, -1, 63,         // 62 is placed at index 43 (for +), 63 at index 47 (for /)
            52, 53, 54, 55, 56, 57, 58, 59, 60, 61, -1, -1, -1, -1, -1, -1,         // 52-61 are placed at index 48-57 (for 0-9), 64 at index 61 (for =)
            -1, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14,
            15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, -1, -1, -1, -1, -1,         // 0-25 are placed at index 65-90 (for A-Z)
            -1, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40,
            41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, -1, -1, -1, -1, -1,         // 26-51 are placed at index 97-122 (for a-z)
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,         // Bytes over 122 ('z') are invalid and cannot be decoded
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,         // Hence, padding the map with 255, which indicates invalid input
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
            -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1,
        };
    }
}
