//-----------------------------------------------------------------------
// <copyright file="BitUtils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// List of utility for doing bit (hacks).
    /// </summary>
    internal static class BitUtils
    {
        /// <summary>
        /// https://stackoverflow.com/questions/11376288/fast-computing-of-log2-for-64-bit-integers
        /// </summary>
        private static readonly int[] tab64 = new int[]
        {
            63,  0, 58,  1, 59, 47, 53,  2,
            60, 39, 48, 27, 54, 33, 42,  3,
            61, 51, 37, 40, 49, 18, 28, 20,
            55, 30, 34, 11, 43, 14, 22,  4,
            62, 57, 46, 52, 38, 26, 32, 41,
            50, 36, 17, 19, 29, 10, 13, 21,
            56, 45, 25, 31, 35, 16,  9, 12,
            44, 24, 15,  8, 23,  7,  6,  5
        };

        public static long GetMostSignificantBit(long x)
        {
            // http://aggregate.org/MAGIC/#Most%20Significant%201%20Bit
            // Given a binary integer value x, the most significant 1 bit (highest numbered element of a bit set)
            // can be computed using a SWAR algorithm that recursively "folds" the upper bits into the lower bits.
            // This process yields a bit vector with the same most significant 1 as x, but all 1's below it.
            // Bitwise AND of the original value with the complement of the "folded" value shifted down by one yields the most significant bit.
            // For a 64-bit value:

            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            x |= (x >> 32);
            return (x & ~(x >> 1));
        }

        public static int FloorLog2(ulong value)
        {
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            value |= value >> 32;
            return tab64[((ulong)((value - (value >> 1)) * 0x07EDD5E59A4E28C2)) >> 58];
        }

        public static bool IsPowerOf2(ulong x)
        {
            return (x & (x - 1)) == 0;
        }

        public static int GetMostSignificantBitIndex(ulong x)
        {
            return BitUtils.FloorLog2(x);
        }

        public static long GetLeastSignificantBit(long x)
        {
            // http://aggregate.org/MAGIC/#Least%20Significant%201%20Bit
            // Given a 2's complement binary integer value x, (x&-x) is the least significant 1 bit.
            // (This was pointed-out by Tom May.)
            // The reason this works is that it is equivalent to (x & ((~x) + 1));
            // any trailing zero bits in x become ones in ~x, 
            // adding 1 to that carries into the following bit,
            // and AND with x yields only the flipped bit... the original position of the least significant 1 bit.

            return (int)(x & -x);
        }

        public static int GetLeastSignificantBitIndex(long x)
        {
            return BitUtils.FloorLog2((ulong)BitUtils.GetLeastSignificantBit(x));
        }

        //examines bit b of the address a, returns its current value, and resets the bit to 0.
        public static bool BitTestAndReset64(long input, int index, out long output)
        {
            // https://stackoverflow.com/questions/2431732/checking-if-a-bit-is-set-or-not
            bool set = (input & (1L << index)) != 0;

            // https://www.dotnetperls.com/set-bit-zero
            output = (input &= ~(1L << index));

            return set;
        }
    }
}
