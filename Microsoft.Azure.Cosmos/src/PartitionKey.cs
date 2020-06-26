//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Represents a partition key value in the Azure Cosmos DB service.
    /// </summary>
    public readonly struct PartitionKey : IEquatable<PartitionKey>
    {
        private static readonly PartitionKeyInternal NullPartitionKeyInternal = new Documents.PartitionKey(null).InternalKey;
        private static readonly PartitionKeyInternal TruePartitionKeyInternal = new Documents.PartitionKey(true).InternalKey;
        private static readonly PartitionKeyInternal FalsePartitionKeyInternal = new Documents.PartitionKey(false).InternalKey;

        /// <summary>
        /// The returned object represents a partition key value that allows creating and accessing items
        /// without a value for partition key.
        /// </summary>
        public static readonly PartitionKey None = new PartitionKey(Documents.PartitionKey.None.InternalKey, true);

        /// <summary>
        /// The returned object represents a partition key value that allows creating and accessing items
        /// with a null value for the partition key.
        /// </summary>
        public static readonly PartitionKey Null = new PartitionKey(PartitionKey.NullPartitionKeyInternal);

        /// <summary>
        /// The tag name to use in the documents for specifying a partition key value
        /// when inserting such documents into a migrated collection
        /// </summary>
        public static readonly string SystemKeyName = Documents.PartitionKey.SystemKeyName;

        /// <summary>
        /// The partition key path in the collection definition for migrated collections
        /// </summary>
        public static readonly string SystemKeyPath = Documents.PartitionKey.SystemKeyPath;

        /// <summary>
        /// Gets the value provided at initialization.
        /// </summary>
        internal PartitionKeyInternal InternalKey { get; }

        /// <summary>
        /// Gets the boolean to verify partitionKey is None.
        /// </summary>
        internal bool IsNone { get; }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(string partitionKeyValue)
        {
            if (partitionKeyValue == null)
            {
                this.InternalKey = PartitionKey.NullPartitionKeyInternal;
            }
            else
            {
                this.InternalKey = new Documents.PartitionKey(partitionKeyValue).InternalKey;
            }
            this.IsNone = false;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(bool partitionKeyValue)
        {
            this.InternalKey = partitionKeyValue ? TruePartitionKeyInternal : FalsePartitionKeyInternal;
            this.IsNone = false;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(double partitionKeyValue)
        {
            this.InternalKey = new Documents.PartitionKey(partitionKeyValue).InternalKey;
            this.IsNone = false;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="value">The value to use as partition key.</param>
        internal PartitionKey(object value)
        {
            this.InternalKey = new Documents.PartitionKey(value).InternalKey;
            this.IsNone = false;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="value">The value to use as partition key.</param>
        internal PartitionKey(PartitionKeyValueList value)
        {
            object[] valueArray = value.partitionKeyObjects.ToArray();
            this.InternalKey = new Documents.PartitionKey(valueArray).InternalKey;
            this.IsNone = false;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyInternal">The value to use as partition key.</param>
        /// <param name="isNone">The value to decide partitionKey is None.</param>
        private PartitionKey(PartitionKeyInternal partitionKeyInternal, bool isNone = false)
        {
            this.InternalKey = partitionKeyInternal;
            this.IsNone = isNone;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="obj">An object to compare.</param>
        /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
        public override bool Equals(object obj)
        {
            if (obj is PartitionKey partitionkey)
            {
                return this.Equals(partitionkey);
            }

            return false;
        }

        /// <summary>
        /// Returns the hash code for this partition key.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode()
        {
            if (this.InternalKey == null)
            {
                return PartitionKey.NullPartitionKeyInternal.GetHashCode();
            }

            return this.InternalKey.GetHashCode();
        }

        /// <summary>
        /// Returns a value indicating whether this instance is equal to a specified partition key.
        /// </summary>
        /// <param name="other">A partition key value to compare to this instance.</param>
        /// <returns>true if <paramref name="other"/> has the same value as this instance; otherwise, false.</returns>
        public bool Equals(PartitionKey other)
        {
            PartitionKeyInternal partitionKeyInternal = this.InternalKey;
            PartitionKeyInternal otherPartitionKeyInternal = other.InternalKey;
            if (partitionKeyInternal == null)
            {
                partitionKeyInternal = PartitionKey.NullPartitionKeyInternal;
            }

            if (otherPartitionKeyInternal == null)
            {
                otherPartitionKeyInternal = PartitionKey.NullPartitionKeyInternal;
            }

            return partitionKeyInternal.Equals(otherPartitionKeyInternal);
        }

        /// <summary>
        /// Gets the string representation of the partition key value.
        /// </summary>
        /// <returns>The string representation of the partition key value</returns>
        public override string ToString()
        {
            if (this.InternalKey == null)
            {
                return PartitionKey.NullPartitionKeyInternal.ToJsonString();
            }

            return this.InternalKey.ToJsonString();
        }

        internal string ToJsonString()
        {
            return this.InternalKey.ToJsonString();
        }

        internal static bool TryParseJsonString(string partitionKeyString, out PartitionKey partitionKey)
        {
            if (partitionKeyString == null)
            {
                throw new ArgumentNullException(partitionKeyString);
            }

            try
            {
                PartitionKeyInternal partitionKeyInternal = PartitionKeyInternal.FromJsonString(partitionKeyString);
                if (partitionKeyInternal.Components == null)
                {
                    partitionKey = PartitionKey.None;
                }
                else
                {
                    partitionKey = new PartitionKey(partitionKeyInternal, isNone: false);
                }

                return true;
            }
            catch (Exception)
            {
                partitionKey = default;
                return false;
            }
        }

        /// <summary>
        /// Determines whether two specified instances of the PartitionKey are equal.
        /// </summary>
        /// <param name="left">The first object to compare.</param>
        /// <param name="right">The second object to compare.</param>
        /// <returns>true if <paramref name="left"/> and <paramref name="right"/> represent the same partition key; otherwise, false.</returns>
        public static bool operator ==(PartitionKey left, PartitionKey right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two specified instances of the PartitionKey are not equal.
        /// </summary>
        /// <param name="left">The first object to compare.</param>
        /// <param name="right">The second object to compare.</param>
        /// <returns>true if <paramref name="left"/> and <paramref name="right"/> do not represent the same partition key; otherwise, false.</returns>
        public static bool operator !=(PartitionKey left, PartitionKey right)
        {
            return !left.Equals(right);
        }
    }
}
