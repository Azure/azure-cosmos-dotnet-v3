//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

[module: System.Diagnostics.CodeAnalysis.SuppressMessage("Readability Rules", "SA1135", Justification = "Dual Compilation")]

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.Diagnostics;
    using Documents;

#pragma warning disable SA1649 // File name should match first type name
    internal static class MurmurHash3
#pragma warning restore SA1649 // File name should match first type name
    {
        public static uint Hash32(byte[] bytes, long length, uint seed = 0)
        {
            // MurmurHash3 32bit implementation:
            // https://en.wikipedia.org/wiki/MurmurHash
            uint c1 = 0xcc9e2d51;
            uint c2 = 0x1b873593;

            uint h1 = seed;

            for (int i = 0; i < length - 3; i += 4)
            {
                uint k1 = BitConverter.ToUInt32(bytes, i);

                k1 *= c1;
                k1 = RotateLeft32(k1, 15);
                k1 *= c2;

                h1 ^= k1;
                h1 = RotateLeft32(h1, 13);
                h1 = (h1 * 5) + 0xe6546b64;
            }

            // tail
            uint k = 0;

            switch (length & 3)
            {
                case 3:
                    k ^= (uint)bytes[length - 1] << 16;
                    k ^= (uint)bytes[length - 2] << 8;
                    k ^= (uint)bytes[length - 3];
                    break;

                case 2:
                    k ^= (uint)bytes[length - 1] << 8;
                    k ^= (uint)bytes[length - 2];
                    break;

                case 1:
                    k ^= (uint)bytes[length - 1];
                    break;
            }

            k *= c1;
            k = RotateLeft32(k, 15);
            k *= c2;
            h1 ^= k;

            // finalization
            h1 ^= (uint)length;
            h1 ^= h1 >> 16;
            h1 *= 0x85ebca6b;
            h1 ^= h1 >> 13;
            h1 *= 0xc2b2ae35;
            h1 ^= h1 >> 16;

            return h1;
        }

        public static ulong Hash64(byte[] bytes, int length, ulong seed = 0)
        {
            // MurmurHash2 64-bit implementation:
            // https://en.wikipedia.org/wiki/MurmurHash
            int numBlocks = length / 8;
            const ulong c1 = 0x87c37b91114253d5;
            const ulong c2 = 0x4cf5ad432745937f;

            ulong h1 = seed;

            // body
            int position;
            for (position = 0; position < length - 7; position += 8)
            {
                ulong k1 = BitConverter.ToUInt64(bytes, position);

                // k1, h1
                k1 *= c1;
                k1 = MurmurHash3.RotateLeft64(k1, 31);
                k1 *= c2;

                h1 ^= k1;
                h1 = MurmurHash3.RotateLeft64(h1, 27);
                h1 = (h1 * 5) + 0x52dce729;
            }

            // tail
            {
                ulong k1 = 0;

                switch (length & 7)
                {
                    case 7:
                        k1 ^= ((ulong)bytes[position + 6]) << 48;
                        break;
                    case 6:
                        k1 ^= ((ulong)bytes[position + 5]) << 40;
                        break;
                    case 5:
                        k1 ^= ((ulong)bytes[position + 4]) << 32;
                        break;
                    case 4:
                        k1 ^= ((ulong)bytes[position + 3]) << 24;
                        break;
                    case 3:
                        k1 ^= ((ulong)bytes[position + 2]) << 16;
                        break;
                    case 2:
                        k1 ^= ((ulong)bytes[position + 1]) << 8;
                        break;
                    case 1:
                        k1 ^= ((ulong)bytes[position + 0]) << 0;
                        break;
                    default:
                        break;
                }

                k1 *= c1;
                k1 = MurmurHash3.RotateLeft64(k1, 31);
                k1 *= c2;
                h1 ^= k1;
            }

            // finalization
            h1 ^= (ulong)length;

            h1 ^= h1 >> 33;
            h1 *= 0xff51afd7ed558ccd;
            h1 ^= h1 >> 33;
            h1 *= 0xc4ceb9fe1a85ec53;
            h1 ^= h1 >> 33;

            return h1;
        }

        public static UInt128 Hash128(byte[] bytes, int length, UInt128 seed)
        {
            const ulong c1 = 0x87c37b91114253d5;
            const ulong c2 = 0x4cf5ad432745937f;

            ulong h1 = seed.GetHigh();
            ulong h2 = seed.GetLow();

            // body
            int position;
            for (position = 0; position < length - 15; position += 16)
            {
                ulong k1 = BitConverter.ToUInt64(bytes, position);
                ulong k2 = BitConverter.ToUInt64(bytes, position + 8);

                // k1, h1
                k1 *= c1;
                k1 = RotateLeft64(k1, 31);
                k1 *= c2;

                h1 ^= k1;
                h1 = RotateLeft64(h1, 27);
                h1 += h2;
                h1 = (h1 * 5) + 0x52dce729;

                // k2, h2
                k2 *= c2;
                k2 = RotateLeft64(k2, 33);
                k2 *= c1;

                h2 ^= k2;
                h2 = RotateLeft64(h2, 31);
                h2 += h1;
                h2 = (h2 * 5) + 0x38495ab5;
            }

            {
                // tail
                ulong k1 = 0;
                ulong k2 = 0;

                int n = length & 15;
#pragma warning disable SA1503 // Braces should not be omitted
                if (n >= 15) k2 ^= ((ulong)bytes[position + 14]) << 48;
                if (n >= 14) k2 ^= ((ulong)bytes[position + 13]) << 40;
                if (n >= 13) k2 ^= ((ulong)bytes[position + 12]) << 32;
                if (n >= 12) k2 ^= ((ulong)bytes[position + 11]) << 24;
                if (n >= 11) k2 ^= ((ulong)bytes[position + 10]) << 16;
                if (n >= 10) k2 ^= ((ulong)bytes[position + 09]) << 8;
                if (n >= 9) k2 ^= ((ulong)bytes[position + 08]) << 0;
#pragma warning restore SA1503 // Braces should not be omitted

                k2 *= c2;
                k2 = RotateLeft64(k2, 33);
                k2 *= c1;
                h2 ^= k2;

#pragma warning disable SA1503 // Braces should not be omitted
                if (n >= 8) k1 ^= ((ulong)bytes[position + 7]) << 56;
                if (n >= 7) k1 ^= ((ulong)bytes[position + 6]) << 48;
                if (n >= 6) k1 ^= ((ulong)bytes[position + 5]) << 40;
                if (n >= 5) k1 ^= ((ulong)bytes[position + 4]) << 32;
                if (n >= 4) k1 ^= ((ulong)bytes[position + 3]) << 24;
                if (n >= 3) k1 ^= ((ulong)bytes[position + 2]) << 16;
                if (n >= 2) k1 ^= ((ulong)bytes[position + 1]) << 8;
                if (n >= 1) k1 ^= ((ulong)bytes[position + 0]) << 0;
#pragma warning restore SA1503 // Braces should not be omitted

                k1 *= c1;
                k1 = RotateLeft64(k1, 31);
                k1 *= c2;
                h1 ^= k1;
            }

            // finalization
            h1 ^= (ulong)length;
            h2 ^= (ulong)length;

            h1 += h2;
            h2 += h1;

            // h1
            h1 ^= h1 >> 33;
            h1 *= 0xff51afd7ed558ccd;
            h1 ^= h1 >> 33;
            h1 *= 0xc4ceb9fe1a85ec53;
            h1 ^= h1 >> 33;

            // h2
            h2 ^= h2 >> 33;
            h2 *= 0xff51afd7ed558ccd;
            h2 ^= h2 >> 33;
            h2 *= 0xc4ceb9fe1a85ec53;
            h2 ^= h2 >> 33;

            h1 += h2;
            h2 += h1;

            if (!BitConverter.IsLittleEndian)
            {
                h1 = MurmurHash3.Reverse(h1);
                h2 = MurmurHash3.Reverse(h2);
            }

            return UInt128.Create(h1, h2);
        }

        public static ulong Reverse(ulong value)
        {
            ulong b1 = (value >> 0) & 0xff;
            ulong b2 = (value >> 8) & 0xff;
            ulong b3 = (value >> 16) & 0xff;
            ulong b4 = (value >> 24) & 0xff;
            ulong b5 = (value >> 32) & 0xff;
            ulong b6 = (value >> 40) & 0xff;
            ulong b7 = (value >> 48) & 0xff;
            ulong b8 = (value >> 56) & 0xff;

            return b1 << 56 | b2 << 48 | b3 << 40 | b4 << 32 | b5 << 24 | b6 << 16 | b7 << 8 | b8 << 0;
        }

        private static uint RotateLeft32(uint n, int numBits)
        {
            Debug.Assert(numBits < 32, "numBits < 32");
            return (n << numBits) | (n >> (32 - numBits));
        }

        private static ulong RotateLeft64(ulong n, int numBits)
        {
            Debug.Assert(numBits < 64, "numBits < 64");
            return (n << numBits) | (n >> (64 - numBits));
        }
    }
}