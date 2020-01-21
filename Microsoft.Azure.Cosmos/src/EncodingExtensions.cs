// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Text;

    internal static class EncodingExtensions
    {
        public static int GetByteCount(this Encoding encoding, string chars)
        {
            return encoding.GetByteCount(chars.AsSpan());
        }

        public static unsafe int GetByteCount(this Encoding encoding, ReadOnlySpan<char> chars)
        {
            if (chars.IsEmpty)
            {
                return 0;
            }

            fixed (char* charPointer = &chars.GetPinnableReference())
            {
                return encoding.GetByteCount(charPointer, chars.Length);
            }
        }

        public static int GetBytes(this Encoding encoding, string src, Span<byte> dst)
        {
            return encoding.GetBytes(src.AsSpan(), dst);
        }

        public static unsafe int GetBytes(this Encoding encoding, ReadOnlySpan<char> src, Span<byte> dst)
        {
            if (src.IsEmpty)
            {
                return 0;
            }

            fixed (char* chars = &src.GetPinnableReference())
            fixed (byte* bytes = &dst.GetPinnableReference())
            {
                return encoding.GetBytes(chars, src.Length, bytes, dst.Length);
            }
        }

        public static unsafe int GetCharCount(this Encoding encoding, ReadOnlySpan<byte> src)
        {
            if (src.IsEmpty)
            {
                return 0;
            }

            fixed (byte* bytes = &src.GetPinnableReference())
            {
                return encoding.GetCharCount(bytes, src.Length);
            }
        }

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
    }
}
