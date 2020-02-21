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
    internal readonly struct EffectivePartitionKey : IComparable<EffectivePartitionKey>, IEquatable<EffectivePartitionKey>
    {
        public EffectivePartitionKey(UInt128 value)
        {
            this.Value = value;
        }

        public UInt128 Value { get; }

        public int CompareTo(EffectivePartitionKey other)
        {
            return this.Value.CompareTo(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EffectivePartitionKey effectivePartitionKey))
            {
                return false;
            }

            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }

            return this.Equals(effectivePartitionKey);
        }

        public bool Equals(EffectivePartitionKey other)
        {
            return this.Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return this.Value.GetHashCode();
        }

        public static class V1
        {
            private const int MaxStringLength = 100;

            private static readonly EffectivePartitionKey True = EffectivePartitionKey.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.True });
            private static readonly EffectivePartitionKey False = EffectivePartitionKey.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.False });
            private static readonly EffectivePartitionKey Null = EffectivePartitionKey.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.Null });
            private static readonly EffectivePartitionKey Undefined = EffectivePartitionKey.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.Undefined });
            private static readonly EffectivePartitionKey EmptyString = EffectivePartitionKey.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.String });

            public static EffectivePartitionKey Hash(bool boolean)
            {
                return boolean ? EffectivePartitionKey.V1.True : EffectivePartitionKey.V1.False;
            }

            public static EffectivePartitionKey Hash(double value)
            {
                Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + sizeof(double)];
                bytesForHashing[0] = (byte)PartitionKeyComponentType.Number;
                MemoryMarshal.Cast<byte, double>(bytesForHashing.Slice(start: 1))[0] = value;
                return EffectivePartitionKey.V1.Hash(bytesForHashing);
            }

            public static EffectivePartitionKey Hash(string value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Length == 0)
                {
                    return EmptyString;
                }

                ReadOnlySpan<char> trimmedValue = value.AsSpan(
                    start: 0,
                    length: Math.Min(value.Length, MaxStringLength));

                Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + Encoding.UTF8.GetByteCount(trimmedValue)];
                bytesForHashing[0] = (byte)PartitionKeyComponentType.String;
                Span<byte> bytesForHashingSuffix = bytesForHashing.Slice(start: 1);
                Encoding.UTF8.GetBytes(
                    trimmedValue,
                    bytesForHashingSuffix);
                return EffectivePartitionKey.V1.Hash(bytesForHashing);
            }

            public static EffectivePartitionKey HashNull()
            {
                return EffectivePartitionKey.V1.Null;
            }

            public static EffectivePartitionKey HashUndefined()
            {
                return EffectivePartitionKey.V1.Undefined;
            }

            private static EffectivePartitionKey Hash(ReadOnlySpan<byte> bytesForHashing)
            {
                uint hash = Cosmos.MurmurHash3.Hash32(bytesForHashing, seed: 0);
                return new EffectivePartitionKey(hash);
            }
        }

        public static class V2
        {
            private const int MaxStringLength = 2 * 1024;
            private static readonly EffectivePartitionKey True = EffectivePartitionKey.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.True });
            private static readonly EffectivePartitionKey False = EffectivePartitionKey.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.False });
            private static readonly EffectivePartitionKey Null = EffectivePartitionKey.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.Null });
            private static readonly EffectivePartitionKey Undefined = EffectivePartitionKey.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.Undefined });
            private static readonly EffectivePartitionKey EmptyString = EffectivePartitionKey.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.String });

            public static EffectivePartitionKey Hash(bool boolean)
            {
                return boolean ? EffectivePartitionKey.V2.True : EffectivePartitionKey.V2.False;
            }

            public static EffectivePartitionKey Hash(double value)
            {
                Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + sizeof(double)];
                bytesForHashing[0] = (byte)PartitionKeyComponentType.Number;
                MemoryMarshal.Cast<byte, double>(bytesForHashing.Slice(start: 1))[0] = value;
                return EffectivePartitionKey.V2.Hash(bytesForHashing);
            }

            public static EffectivePartitionKey Hash(string value)
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (value.Length > MaxStringLength)
                {
                    throw new ArgumentOutOfRangeException($"{nameof(value)} is too long.");
                }

                if (value.Length == 0)
                {
                    return EmptyString;
                }

                Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + Encoding.UTF8.GetByteCount(value)];
                bytesForHashing[0] = (byte)PartitionKeyComponentType.String;
                Span<byte> bytesForHashingSuffix = bytesForHashing.Slice(start: 1);
                Encoding.UTF8.GetBytes(value, bytesForHashingSuffix);
                return EffectivePartitionKey.V2.Hash(bytesForHashing);
            }

            public static EffectivePartitionKey HashNull()
            {
                return EffectivePartitionKey.V2.Null;
            }

            public static EffectivePartitionKey HashUndefined()
            {
                return EffectivePartitionKey.V2.Undefined;
            }

            private unsafe static EffectivePartitionKey Hash(ReadOnlySpan<byte> bytesForHashing)
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
        }
    }
}
