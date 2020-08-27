//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.SharedFiles.Routing
{
    using System;
    using System.Numerics;

    internal struct Int128
    {
        private readonly BigInteger value;


        private static readonly BigInteger MaxBigIntValue = new BigInteger(
             new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x80 });

        public static readonly Int128 MaxValue = new Int128( new BigInteger(
                new byte[]
                    { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }));

        private Int128(BigInteger value)
        {
            this.value = value % MaxBigIntValue;
        }

        public static implicit operator Int128(int n)
        {
            return new Int128(new BigInteger(n));
        }

        public Int128(byte[] data)
        {
            if (data.Length != 16)
            {
                throw new ArgumentException("data");
            }

            this.value = new BigInteger(data);

            if (this.value > MaxValue.value)
            {
                throw new ArgumentException();
            }
        }

        public static Int128 operator *(Int128 left, Int128 right)
        {
            return new Int128(left.value * right.value);
        }

        public static Int128 operator +(Int128 left, Int128 right)
        {
            return new Int128(left.value + right.value);
        }

        public static Int128 operator -(Int128 left, Int128 right)
        {
            return new Int128(left.value - right.value);
        }

        public static Int128 operator / (Int128 left, Int128 right)
        {
            return new Int128(left.value / right.value);
        }

        public static bool operator >(Int128 left, Int128 right)
        {
            return left.value > right.value;
        }

        public static bool operator <(Int128 left, Int128 right)
        {
            return left.value < right.value;
        }

        public byte[] Bytes
        {
            get
            {
                byte[] bytes = this.value.ToByteArray();
                if (bytes.Length < 16)
                {
                    byte[] paddedBytes = new byte[16];
                    Buffer.BlockCopy(bytes, 0, paddedBytes, 0, bytes.Length);
                    return paddedBytes;
                }

                return bytes;
            }
        }
    }
}
