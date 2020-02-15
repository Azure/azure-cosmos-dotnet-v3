// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Drawing;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Cosmos.Sql;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// There are many kinds of documents partitioning schemes (Range, Hash, Range+Hash, Hash+Hash etc.)
    /// All these partitioning schemes are abstracted by effective partition key.
    /// Effective partition key is just BYTE*. There is function which maps partition key to effective partition key based on partitioning scheme used.
    /// In case of range partitioning effective partition key corresponds one-to-one to partition key extracted from the document and relationship between effective partition keys is the same as relationship between partitionkeys from which they were calculated.
    /// In case of Hash partitioning, values of all paths are hashed together and resulting hash is prepended to the partition key.
    /// We have single global index on [effective partition key + name]
    /// </summary>
    /// <example>
    /// With the following definition:
    ///     "partitionKey" : {"paths":["/address/country", "address/zipcode"], "kind" : "Hash"}
    /// partition key ["USA", 98052] corresponds to effective partition key binaryencode([243451234, "USA", 98052]), where
    /// 243451234 is hash of "USA" and 98052 combined together.
    /// 
    /// With the following definition:
    ///     "partitionKey" : {"paths":["/address/country", "address/zipcode"], "kind" : "Range"}
    /// partition key ["USA", 98052] corresponds to effective partition key binaryencode(["USA", 98052]).
    /// </example>
    internal readonly struct EffectivePartitionKey
    {
        private const int HashV1MaxStringLength = 100;

        private static readonly EffectivePartitionKey TrueV1 = EffectivePartitionKey.HashV1(new byte[] { (byte)PartitionKeyComponentType.True });
        private static readonly EffectivePartitionKey FalseV1 = EffectivePartitionKey.HashV1(new byte[] { (byte)PartitionKeyComponentType.False });
        private static readonly EffectivePartitionKey NullV1 = EffectivePartitionKey.HashV1(new byte[] { (byte)PartitionKeyComponentType.Null });
        private static readonly EffectivePartitionKey UndefinedV1 = EffectivePartitionKey.HashV1(new byte[] { (byte)PartitionKeyComponentType.Undefined });

        private static readonly EffectivePartitionKey TrueV2 = EffectivePartitionKey.HashV2(new byte[] { (byte)PartitionKeyComponentType.True });
        private static readonly EffectivePartitionKey FalseV2 = EffectivePartitionKey.HashV2(new byte[] { (byte)PartitionKeyComponentType.False });
        private static readonly EffectivePartitionKey NullV2 = EffectivePartitionKey.HashV2(new byte[] { (byte)PartitionKeyComponentType.Null });
        private static readonly EffectivePartitionKey UndefinedV2 = EffectivePartitionKey.HashV2(new byte[] { (byte)PartitionKeyComponentType.Undefined });

        private EffectivePartitionKey(UInt128 bits)
        {
            this.Bits = bits;
        }

        private UInt128 Bits { get; }

        #region HashV1
        public static EffectivePartitionKey HashV1(bool boolean)
        {
            return boolean ? EffectivePartitionKey.TrueV1 : EffectivePartitionKey.FalseV1;
        }

        public static EffectivePartitionKey HashV1(double value)
        {
            Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + sizeof(double)];
            bytesForHashing[0] = (byte)PartitionKeyComponentType.Number;
            MemoryMarshal.Cast<byte, double>(bytesForHashing.Slice(start: 1))[0] = value;
            return EffectivePartitionKey.HashV1(bytesForHashing);
        }

        public static EffectivePartitionKey HashV1(string value)
        {
            Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + Encoding.UTF8.GetByteCount(value.AsSpan(), count: 100)];
            bytesForHashing[0] = (byte)PartitionKeyComponentType.String;
            Span<byte> bytesForHashingSuffix = bytesForHashing.Slice(start: 1);
            Encoding.UTF8.GetBytes(value, HashV1MaxStringLength, bytesForHashingSuffix, bytesForHashingSuffix.Length);
            return EffectivePartitionKey.HashV1(bytesForHashing);
        }

        public static EffectivePartitionKey HashNullV1()
        {
            return EffectivePartitionKey.NullV1;
        }

        public static EffectivePartitionKey HashUndefinedV1()
        {
            return EffectivePartitionKey.UndefinedV1;
        }

        private static EffectivePartitionKey HashV1(ReadOnlySpan<byte> bytesForHashing)
        {
            uint hash = Cosmos.MurmurHash3.Hash32(bytesForHashing, seed: 0);
            return new EffectivePartitionKey(hash);
        }
        #endregion
        #region HashV2
        public static EffectivePartitionKey HashV2(bool boolean)
        {
            return boolean ? EffectivePartitionKey.TrueV2 : EffectivePartitionKey.FalseV2;
        }

        public static EffectivePartitionKey HashV2(double value)
        {
            Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + sizeof(double)];
            bytesForHashing[0] = (byte)PartitionKeyComponentType.Number;
            MemoryMarshal.Cast<byte, double>(bytesForHashing.Slice(start: 1))[0] = value;
            return EffectivePartitionKey.HashV2(bytesForHashing);
        }

        public static EffectivePartitionKey HashV2(string value)
        {
            Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + Encoding.UTF8.GetByteCount(value.AsSpan(), count: 100)];
            bytesForHashing[0] = (byte)PartitionKeyComponentType.String;
            Span<byte> bytesForHashingSuffix = bytesForHashing.Slice(start: 1);
            Encoding.UTF8.GetBytes(value, HashV1MaxStringLength, bytesForHashingSuffix, bytesForHashingSuffix.Length);
            return EffectivePartitionKey.HashV2(bytesForHashing);
        }

        public static EffectivePartitionKey HashNullV2()
        {
            return EffectivePartitionKey.NullV2;
        }

        public static EffectivePartitionKey HashUndefinedV2()
        {
            return EffectivePartitionKey.UndefinedV2;
        }

        private unsafe static EffectivePartitionKey HashV2(ReadOnlySpan<byte> bytesForHashing)
        {
            UInt128 hash = Cosmos.MurmurHash3.Hash128(bytesForHashing, seed: 0);
            ReadOnlySpan<UInt128> readSpan = new ReadOnlySpan<UInt128>(&hash, 1);
            Span<byte> hashBytes = stackalloc byte[sizeof(UInt128)];
            MemoryMarshal.AsBytes(readSpan).CopyTo(hashBytes);
            MemoryExtensions.Reverse(hashBytes);
            // Reset 2 most significant bits, as max exclusive value is 'FF'.
            // Plus one more just in case.
            hashBytes[0] &= 0x3F;
            hash = MemoryMarshal.Cast<byte, UInt128>(hashBytes)[0];
            return new EffectivePartitionKey(hash);
        }
        #endregion
    }
}
