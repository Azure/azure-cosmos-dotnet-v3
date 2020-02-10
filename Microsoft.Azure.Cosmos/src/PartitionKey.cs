//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Represents a partition key value in the Azure Cosmos DB service.
    /// </summary>
    public readonly struct PartitionKey
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
        /// <param name="partitionKeyInternal">The value to use as partition key.</param>
        /// <param name="isNone">The value to decide partitionKey is None.</param>
        private PartitionKey(PartitionKeyInternal partitionKeyInternal, bool isNone = false)
        {
            this.InternalKey = partitionKeyInternal;
            this.IsNone = isNone;
        }

        /// <summary>
        /// Gets the string representation of the partition key value.
        /// </summary>
        /// <returns>The string representation of the partition key value</returns>
        public override string ToString()
        {
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
    }
}
