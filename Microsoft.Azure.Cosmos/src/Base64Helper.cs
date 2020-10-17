//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Runtime.InteropServices;
    using System.Security;
    using Microsoft.Azure.Documents;

    internal static class Base64Helper
    {
        /// <summary>
        /// Interprets <paramref name="secureString"/> as a Base64 string, and decodes it into a native byte array,
        /// which it returns.
        /// Avoids loading either the original Base64 or decoded binary into managed heap.
        /// </summary>
        /// <param name="secureString">Base64 string to decode</param>
        /// <param name="secureStringLength">Length of the Base64 string to decode</param>
        /// <param name="bytes">
        ///   An IntPtr allocated with Marshal.AllocCoTaskMem, which, when the user is done, 
        ///   MUST be zeroed out and then freed with Marshal.FreeCoTaskMem by the caller.
        /// </param>
        /// <param name="bytesLength">Number of bytes in the decoded binary currentCharacter</param>
        public static void SecureStringToNativeBytes(SecureString secureString, int secureStringLength, out IntPtr bytes, out uint bytesLength)
        {
            IntPtr nativeBytes = IntPtr.Zero;
            try
            {
                nativeBytes = Marshal.AllocCoTaskMem(secureStringLength);
                uint actualLength = 0;
                Base64Helper.ParseStringToIntPtr(secureString, nativeBytes, secureStringLength, out actualLength);

                bytes = nativeBytes;
                bytesLength = actualLength;
            }
            catch
            {
                if (nativeBytes != IntPtr.Zero)
                {
                    for (int n = 0; n < secureStringLength; n++)
                    {
                        Marshal.WriteByte(nativeBytes, n, 0);
                    }

                    Marshal.FreeCoTaskMem(nativeBytes);
                }

                nativeBytes = IntPtr.Zero;
                bytes = IntPtr.Zero;
                bytesLength = 0;

                throw;
            }
        }

        private static void ParseStringToIntPtr(SecureString secureString, IntPtr bytes, int allocationSize, out uint actualLength)
        {
            IntPtr unmanagedString = IntPtr.Zero;
            try
            {
                unmanagedString = CustomTypeExtensions.SecureStringToCoTaskMemAnsi(secureString);

                int currentReadOffset = 0;
                int currentWriteOffset = 0;

                byte currentCharacter = 0;
                while (currentReadOffset < allocationSize &&
                    (currentCharacter = Marshal.ReadByte(unmanagedString, currentReadOffset)) != '\0')
                {
                    uint currentValue = 0;
                    int nBits = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        if (currentReadOffset >= allocationSize)
                            break;

                        currentCharacter = Marshal.ReadByte(unmanagedString, currentReadOffset);
                        int valueOfCharacter = 0;
                        {
                            if (currentCharacter >= 'A' && currentCharacter <= 'Z')
                                valueOfCharacter = currentCharacter - 'A' + 0;    // 0 range starts at 'A'
                            else if (currentCharacter >= 'a' && currentCharacter <= 'z')
                                valueOfCharacter = currentCharacter - 'a' + 26;    // 26 range starts at 'a'
                            else if (currentCharacter >= '0' && currentCharacter <= '9')
                                valueOfCharacter = currentCharacter - '0' + 52;    // 52 range starts at '0'
                            else if (currentCharacter == '+')
                                valueOfCharacter = 62;
                            else if (currentCharacter == '/')
                                valueOfCharacter = 63;
                            else
                                valueOfCharacter = -1;
                        }

                        currentReadOffset++;
                        if (valueOfCharacter == -1)
                        {
                            // skip this char - be tolerant of Base64 encodings that insert CR/LF for legibility
                            i--;
                            continue;
                        }

                        currentValue <<= 6;
                        currentValue |= (byte)valueOfCharacter;
                        nBits += 6;
                    }

                    // Ensure space in the byte buffer attempting to write to it.
                    if (currentWriteOffset + (nBits / 8) > (allocationSize))
                    {
                        throw new ArgumentException(nameof(allocationSize));
                    }

                    // Serialize the collected value into the byte buffer, left to right
                    currentValue <<= 24 - nBits;
                    for (int i = 0; i < nBits / 8; i++)
                    {
                        Marshal.WriteByte(bytes, currentWriteOffset, (byte)((currentValue & 0x00ff0000) >> 16));
                        currentWriteOffset++;
                        currentValue <<= 8;
                    }
                }

                actualLength = (uint)currentWriteOffset;
            }
            finally
            {
                if (unmanagedString != IntPtr.Zero)
                {
                    Marshal.ZeroFreeCoTaskMemAnsi(unmanagedString);
                    unmanagedString = IntPtr.Zero;
                }
            }
        }
    }
}
