// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// There are many kinds of documents partitioning schemes (Range, Hash, Range+Hash, Hash+Hash etc.)
    /// All these partitioning schemes are abstracted by effective partition key.
    /// Effective partition key is just BYTE*.
    /// There is function which maps partition key to effective partition key based on partitioning scheme used.
    /// In case of range partitioning
    /// effective partition key corresponds one-to-one to partition key extracted from the document and
    /// relationship between effective partition keys is the same as relationship between partitionkeys from which they were calculated.
    /// In case of Hash partitioning, values of all paths are hashed together and resulting hash is prepended to the partition key.
    /// We have single global index on [effective partition key + id]
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
    ///
    /// In general this struct represents that hashing logic, which used to generate EPK Ranges on the client side.
    /// </example>
    internal readonly struct PartitionKeyHash : IComparable<PartitionKeyHash>, IEquatable<PartitionKeyHash>
    {
        private readonly IReadOnlyList<UInt128> values;

        public PartitionKeyHash(UInt128 value)
            : this(new UInt128[] { value })
        {
        }

        public PartitionKeyHash(UInt128[] values)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (UInt128 value in values)
            {
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append('-');
                }
                stringBuilder.Append(value.ToString());
            }

            this.Value = stringBuilder.ToString();
            this.values = values;
        }

        public readonly static PartitionKeyHash None = new PartitionKeyHash(0);

        public string Value { get; }

        internal readonly IReadOnlyList<UInt128> HashValues => this.values;

        public int CompareTo(PartitionKeyHash other)
        {
            return this.Value.CompareTo(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (!(obj is PartitionKeyHash effectivePartitionKey))
            {
                return false;
            }

#pragma warning disable CA2013 // value boxing, also this check seems redundant (future todo)
            if (object.ReferenceEquals(this, obj))
            {
                return true;
            }
#pragma warning restore CA2013

            return this.Equals(effectivePartitionKey);
        }

        public bool Equals(PartitionKeyHash other)
        {
            return this.Value.Equals(other.Value);
        }

        public override int GetHashCode()
        {
            return this.Value.GetHashCode();
        }

        public override string ToString()
        {
            return this.Value;
        }

        public static bool TryParse(string value, out PartitionKeyHash parsedValue)
        {
            if (!UInt128.TryParse(value, out UInt128 uInt128))
            {
                parsedValue = default;
                return false;
            }

            parsedValue = new PartitionKeyHash(uInt128);
            return true;
        }

        public static PartitionKeyHash Parse(string value)
        {
            if (!PartitionKeyHash.TryParse(value, out PartitionKeyHash parsedValue))
            {
                throw new FormatException();
            }

            return parsedValue;
        }

        public static class V1
        {
            private const int MaxStringLength = 100;

            private static readonly PartitionKeyHash True = PartitionKeyHash.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.True });
            private static readonly PartitionKeyHash False = PartitionKeyHash.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.False });
            private static readonly PartitionKeyHash Null = PartitionKeyHash.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.Null });
            private static readonly PartitionKeyHash Undefined = PartitionKeyHash.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.Undefined });
            private static readonly PartitionKeyHash EmptyString = PartitionKeyHash.V1.Hash(new byte[] { (byte)PartitionKeyComponentType.String });

            public static PartitionKeyHash Hash(bool value)
            {
                return value ? PartitionKeyHash.V1.True : PartitionKeyHash.V1.False;
            }

            public static PartitionKeyHash Hash(double value)
            {
                Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + sizeof(double)];
                bytesForHashing[0] = (byte)PartitionKeyComponentType.Number;
                MemoryMarshal.Cast<byte, double>(bytesForHashing[1..])[0] = value;
                return PartitionKeyHash.V1.Hash(bytesForHashing);
            }

            public static PartitionKeyHash Hash(string value)
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
                Span<byte> bytesForHashingSuffix = bytesForHashing[1..];
                Encoding.UTF8.GetBytes(
                    trimmedValue,
                    bytesForHashingSuffix);
                return PartitionKeyHash.V1.Hash(bytesForHashing);
            }

            public static PartitionKeyHash HashNull()
            {
                return PartitionKeyHash.V1.Null;
            }

            public static PartitionKeyHash HashUndefined()
            {
                return PartitionKeyHash.V1.Undefined;
            }

            private static PartitionKeyHash Hash(ReadOnlySpan<byte> bytesForHashing)
            {
                uint hash = Cosmos.MurmurHash3.Hash32(bytesForHashing, seed: 0);
                return new PartitionKeyHash(hash);
            }
        }

        public static class V2
        {
            private const int MaxStringLength = 2 * 1024;
            private static readonly PartitionKeyHash True = PartitionKeyHash.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.True });
            private static readonly PartitionKeyHash False = PartitionKeyHash.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.False });
            private static readonly PartitionKeyHash Null = PartitionKeyHash.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.Null });
            private static readonly PartitionKeyHash Undefined = PartitionKeyHash.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.Undefined });
            private static readonly PartitionKeyHash EmptyString = PartitionKeyHash.V2.Hash(new byte[] { (byte)PartitionKeyComponentType.String });

            public static PartitionKeyHash Hash(bool value)
            {
                return value ? PartitionKeyHash.V2.True : PartitionKeyHash.V2.False;
            }

            public static PartitionKeyHash Hash(double value)
            {
                Span<byte> bytesForHashing = stackalloc byte[sizeof(byte) + sizeof(double)];
                bytesForHashing[0] = (byte)PartitionKeyComponentType.Number;
                MemoryMarshal.Cast<byte, double>(bytesForHashing[1..])[0] = value;
                return PartitionKeyHash.V2.Hash(bytesForHashing);
            }

            public static PartitionKeyHash Hash(string value)
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
                Span<byte> bytesForHashingSuffix = bytesForHashing[1..];
                Encoding.UTF8.GetBytes(value, bytesForHashingSuffix);
                return PartitionKeyHash.V2.Hash(bytesForHashing);
            }

            public static PartitionKeyHash HashNull()
            {
                return PartitionKeyHash.V2.Null;
            }

            public static PartitionKeyHash HashUndefined()
            {
                return PartitionKeyHash.V2.Undefined;
            }

            private static PartitionKeyHash Hash(ReadOnlySpan<byte> bytesForHashing)
            {
                UInt128 hash = Cosmos.MurmurHash3.Hash128(bytesForHashing, seed: 0);
                return new PartitionKeyHash(hash);
            }
        }

        public static bool operator ==(PartitionKeyHash left, PartitionKeyHash right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PartitionKeyHash left, PartitionKeyHash right)
        {
            return !(left == right);
        }

        public static bool operator <(PartitionKeyHash left, PartitionKeyHash right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(PartitionKeyHash left, PartitionKeyHash right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(PartitionKeyHash left, PartitionKeyHash right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(PartitionKeyHash left, PartitionKeyHash right)
        {
            return left.CompareTo(right) >= 0;
        }
    }
}