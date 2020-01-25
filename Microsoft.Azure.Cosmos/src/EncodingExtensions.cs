// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text;

    internal static class EncodingExtensions
    {
        public static unsafe string GetString(this Encoding encoding, ReadOnlySpan<byte> src)
        {
            if (src.IsEmpty)
            {
                return string.Empty;
            }

            fixed (byte* bytes = &src.GetPinnableReference())
            {
                return encoding.GetString(bytes, src.Length);
            }
        }

        public static unsafe int GetChars(this Encoding encoding, ReadOnlySpan<byte> src, Span<char> dest)
        {
            if (src.Length == 0)
            {
                return 0;
            }

            fixed (byte* srcPointer = src)
            {
                fixed (char* destPointer = dest)
                {
                    return encoding.GetChars(bytes: srcPointer, byteCount: src.Length, chars: destPointer, charCount: dest.Length);
                }
            }
        }

        public static unsafe int GetBytes(this Encoding encoding, string src, Span<byte> dest)
        {
            if (src.Length == 0)
            {
                return 0;
            }

            fixed (char* charPointer = src)
            {
                fixed (byte* spanPointer = dest)
                {
                    return encoding.GetBytes(chars: charPointer, charCount: src.Length, bytes: spanPointer, byteCount: dest.Length);
                }
            }
        }

        public static unsafe int GetByteCount(this Encoding encoding, ReadOnlySpan<char> src)
        {
            if (src.Length == 0)
            {
                return 0;
            }

            fixed (char* charPointer = src)
            {
                return encoding.GetByteCount(chars: charPointer, src.Length);
            }
        }
    }
}
