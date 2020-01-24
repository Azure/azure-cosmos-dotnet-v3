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

        public static unsafe int GetBytes(this Encoding encoding, string src, Span<byte> dest)
        {
            fixed (char* charPointer = src)
            {
                fixed (byte* spanPointer = dest)
                {
                    return Encoding.UTF8.GetBytes(chars: charPointer, charCount: src.Length, bytes: spanPointer, byteCount: dest.Length);
                }
            }
        }
    }
}
