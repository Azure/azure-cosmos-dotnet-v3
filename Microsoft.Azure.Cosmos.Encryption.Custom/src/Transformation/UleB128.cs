// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;

    internal static class UleB128
    {
        private const uint MaxValue = 0x0FFFFFFF;

        public static int GetEncodedLength(uint value)
        {
            if (value > MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (value < 0x80)
            {
                return 1;
            }

            if (value < 0x4000)
            {
                return 2;
            }

            if (value < 0x200000)
            {
                return 3;
            }

            return 4;
        }

        public static int Write(uint value, Span<byte> destination)
        {
            if (value > MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            int length = GetEncodedLength(value);
            if (destination.Length < length)
            {
                throw new ArgumentException("Destination is too small.", nameof(destination));
            }

            uint remaining = value;
            for (int i = 0; i < length; i++)
            {
                byte valueByte = (byte)(remaining & 0x7F);
                remaining >>= 7;
                if (i < length - 1)
                {
                    valueByte |= 0x80;
                }

                destination[i] = valueByte;
            }

            return length;
        }

        public static bool TryRead(ReadOnlySpan<byte> source, out uint value, out int consumed)
        {
            value = 0;
            consumed = 0;

            uint result = 0;
            for (int i = 0; i < 4; i++)
            {
                if (i >= source.Length)
                {
                    return false;
                }

                byte valueByte = source[i];
                result |= (uint)(valueByte & 0x7F) << (7 * i);
                if ((valueByte & 0x80) == 0)
                {
                    if (result > MaxValue || GetEncodedLength(result) != i + 1)
                    {
                        return false;
                    }

                    value = result;
                    consumed = i + 1;
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
