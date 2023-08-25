﻿// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Numerics;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Text;

    /// <summary>
    /// MurmurHash3 for x64 (Little Endian).
    /// <p>Reference: https://en.wikipedia.org/wiki/MurmurHash <br /></p>
    /// <p>
    /// This implementation provides span-based access for hashing content not available in a
    /// <see cref="T:byte[]" />
    /// </p>
    /// </summary>
    internal static class MurmurHash3
    {
        #region Hash32
        /// <summary>MurmurHash3 32-bit implementation for strings.</summary>
        /// <param name="value">The string to hash.</param>
        /// <param name="seed">The seed to initialize with.</param>
        /// <returns>The 32-bit hash.</returns>
        public static uint Hash32(string value, uint seed)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            int size = Encoding.UTF8.GetMaxByteCount(value.Length);
            Span<byte> span = size <= 256 ? stackalloc byte[size] : new byte[size];
            int len = Encoding.UTF8.GetBytes(value, span);
            return MurmurHash3.Hash32(span.Slice(0, len), seed);
        }

        /// <summary>MurmurHash3 32-bit implementation for booleans.</summary>
        /// <param name="value">The data to hash.</param>
        /// <param name="seed">The seed to initialize with.</param>
        /// <returns>The 32-bit hash.</returns>
        public static uint Hash32(bool value, uint seed)
        {
            // Ensure that a bool is ALWAYS a single byte encoding with 1 for true and 0 for false.
            return MurmurHash3.Hash32((byte)(value ? 1 : 0), seed);
        }

        /// <summary>MurmurHash3 32-bit implementation.</summary>
        /// <param name="value">The data to hash.</param>
        /// <param name="seed">The seed to initialize with.</param>
        /// <returns>The 32-bit hash.</returns>
        public static unsafe uint Hash32<T>(T value, uint seed)
            where T : unmanaged
        {
            ReadOnlySpan<T> span = new ReadOnlySpan<T>(&value, 1);
            return MurmurHash3.Hash32(MemoryMarshal.AsBytes(span), seed);
        }

        /// <summary>MurmurHash3 32-bit implementation.</summary>
        /// <param name="span">The data to hash.</param>
        /// <param name="seed">The seed to initialize with.</param>
        /// <returns>The 32-bit hash.</returns>
        public static unsafe uint Hash32(ReadOnlySpan<byte> span, uint seed)
        {
            if (!BitConverter.IsLittleEndian)
            {
                throw new InvalidOperationException("Host machine needs to be little endian.");
            }

            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;

            uint h1 = seed;

            // body
            unchecked
            {
                fixed (byte* bytes = span)
                {
                    for (int i = 0; i < span.Length - 3; i += 4)
                    {
                        uint k1 = *(uint*)(bytes + i);

                        k1 *= c1;
                        k1 = MurmurHash3.RotateLeft32(k1, 15);
                        k1 *= c2;

                        h1 ^= k1;
                        h1 = MurmurHash3.RotateLeft32(h1, 13);
                        h1 = (h1 * 5) + 0xe6546b64;
                    }

                    {
                        // tail
                        uint k = 0;

                        switch (span.Length & 3)
                        {
                            case 3:
                                k ^= (uint)bytes[span.Length - 1] << 16;
                                k ^= (uint)bytes[span.Length - 2] << 8;
                                k ^= (uint)bytes[span.Length - 3];
                                break;

                            case 2:
                                k ^= (uint)bytes[span.Length - 1] << 8;
                                k ^= (uint)bytes[span.Length - 2];
                                break;

                            case 1:
                                k ^= (uint)bytes[span.Length - 1];
                                break;
                        }

                        k *= c1;
                        k = MurmurHash3.RotateLeft32(k, 15);
                        k *= c2;
                        h1 ^= k;
                    }
                }

                // finalization
                h1 ^= (uint)span.Length;
                h1 ^= h1 >> 16;
                h1 *= 0x85ebca6b;
                h1 ^= h1 >> 13;
                h1 *= 0xc2b2ae35;
                h1 ^= h1 >> 16;
            }

            return h1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint RotateLeft32(uint n, int numBits)
        {
            if (numBits >= 32)
            {
                throw new ArgumentOutOfRangeException($"{nameof(numBits)} must be less than 32");
            }

            return (n << numBits) | (n >> (32 - numBits));
        }
        #endregion
        #region Hash128
        /// <summary>MurmurHash3 128-bit implementation.</summary>
        /// <param name="value">The data to hash.</param>
        /// <param name="seed">The seed to initialize with.</param>
        /// <returns>The 128-bit hash represented as two 64-bit words.</returns>
        public static UInt128 Hash128(string value, UInt128 seed)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            int size = Encoding.UTF8.GetMaxByteCount(value.Length);
            Span<byte> span = size <= 256 ? stackalloc byte[size] : new byte[size];
            int len = Encoding.UTF8.GetBytes(value, span);
            return MurmurHash3.Hash128(span.Slice(0, len), seed);
        }

        /// <summary>MurmurHash3 128-bit implementation.</summary>
        /// <param name="value">The data to hash.</param>
        /// <param name="seed">The seed to initialize with.</param>
        /// <returns>The 128-bit hash represented as two 64-bit words.</returns>
        public static UInt128 Hash128(bool value, UInt128 seed)
        {
            // Ensure that a bool is ALWAYS a single byte encoding with 1 for true and 0 for false.
            return MurmurHash3.Hash128((byte)(value ? 1 : 0), seed);
        }

        /// <summary>MurmurHash3 128-bit implementation.</summary>
        /// <param name="value">The data to hash.</param>
        /// <param name="seed">The seed to initialize with.</param>
        /// <returns>The 128-bit hash represented as two 64-bit words.</returns>
        public static unsafe UInt128 Hash128<T>(T value, UInt128 seed)
            where T : unmanaged
        {
            ReadOnlySpan<T> span = new ReadOnlySpan<T>(&value, 1);
            return MurmurHash3.Hash128(MemoryMarshal.AsBytes(span), seed);
        }

        /// <summary>MurmurHash3 128-bit implementation.</summary>
        /// <param name="span">The data to hash.</param>
        /// <param name="seed">The seed to initialize with.</param>
        /// <returns>The 128-bit hash represented as two 64-bit words.</returns>
        public static unsafe UInt128 Hash128(ReadOnlySpan<byte> span, UInt128 seed)
        {
            if (!BitConverter.IsLittleEndian)
            {
                throw new InvalidOperationException("Host machine needs to be little endian.");
            }

            const ulong c1 = 0x87c37b91114253d5;
            const ulong c2 = 0x4cf5ad432745937f;

            (ulong h1, ulong h2) = (seed.GetLow(), seed.GetHigh());

            // body
            unchecked
            {
                fixed (byte* words = span)
                {
                    int position;
                    for (position = 0; position < span.Length - 15; position += 16)
                    {
                        ulong k1 = *(ulong*)(words + position);
                        ulong k2 = *(ulong*)(words + position + 8);

                        // k1, h1
                        k1 *= c1;
                        k1 = MurmurHash3.RotateLeft64(k1, 31);
                        k1 *= c2;

                        h1 ^= k1;
                        h1 = MurmurHash3.RotateLeft64(h1, 27);
                        h1 += h2;
                        h1 = (h1 * 5) + 0x52dce729;

                        // k2, h2
                        k2 *= c2;
                        k2 = MurmurHash3.RotateLeft64(k2, 33);
                        k2 *= c1;

                        h2 ^= k2;
                        h2 = MurmurHash3.RotateLeft64(h2, 31);
                        h2 += h1;
                        h2 = (h2 * 5) + 0x38495ab5;
                    }

                    {
                        // tail
                        ulong k1 = 0;
                        ulong k2 = 0;

                        int n = span.Length & 15;
                        if (n >= 15)
                        {
                            k2 ^= (ulong)words[position + 14] << 48;
                        }

                        if (n >= 14)
                        {
                            k2 ^= (ulong)words[position + 13] << 40;
                        }

                        if (n >= 13)
                        {
                            k2 ^= (ulong)words[position + 12] << 32;
                        }

                        if (n >= 12)
                        {
                            k2 ^= (ulong)words[position + 11] << 24;
                        }

                        if (n >= 11)
                        {
                            k2 ^= (ulong)words[position + 10] << 16;
                        }

                        if (n >= 10)
                        {
                            k2 ^= (ulong)words[position + 09] << 8;
                        }

                        if (n >= 9)
                        {
                            k2 ^= (ulong)words[position + 08] << 0;
                        }

                        k2 *= c2;
                        k2 = MurmurHash3.RotateLeft64(k2, 33);
                        k2 *= c1;
                        h2 ^= k2;

                        if (n >= 8)
                        {
                            k1 ^= (ulong)words[position + 7] << 56;
                        }

                        if (n >= 7)
                        {
                            k1 ^= (ulong)words[position + 6] << 48;
                        }

                        if (n >= 6)
                        {
                            k1 ^= (ulong)words[position + 5] << 40;
                        }

                        if (n >= 5)
                        {
                            k1 ^= (ulong)words[position + 4] << 32;
                        }

                        if (n >= 4)
                        {
                            k1 ^= (ulong)words[position + 3] << 24;
                        }

                        if (n >= 3)
                        {
                            k1 ^= (ulong)words[position + 2] << 16;
                        }

                        if (n >= 2)
                        {
                            k1 ^= (ulong)words[position + 1] << 8;
                        }

                        if (n >= 1)
                        {
                            k1 ^= (ulong)words[position + 0] << 0;
                        }

                        k1 *= c1;
                        k1 = MurmurHash3.RotateLeft64(k1, 31);
                        k1 *= c2;
                        h1 ^= k1;
                    }
                }

                // finalization
                h1 ^= (ulong)span.Length;
                h2 ^= (ulong)span.Length;
                h1 += h2;
                h2 += h1;
                h1 = MurmurHash3.Mix(h1);
                h2 = MurmurHash3.Mix(h2);
                h1 += h2;
                h2 += h1;
            }

            return UInt128.Create(h1, h2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong Mix(ulong h)
        {
            unchecked
            {
                h ^= h >> 33;
                h *= 0xff51afd7ed558ccd;
                h ^= h >> 33;
                h *= 0xc4ceb9fe1a85ec53;
                h ^= h >> 33;
                return h;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ulong RotateLeft64(ulong n, int numBits)
        {
            if (numBits >= 64)
            {
                throw new ArgumentOutOfRangeException($"{nameof(numBits)} must be less than 64");
            }

            return (n << numBits) | (n >> (64 - numBits));
        }
        #endregion
    }
}