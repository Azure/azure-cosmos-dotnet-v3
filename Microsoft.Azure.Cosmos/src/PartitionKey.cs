//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if AZURECORE
namespace Azure.Cosmos
#else
namespace Microsoft.Azure.Cosmos
#endif
{
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Represents a partition key value in the Azure Cosmos DB service.
    /// </summary>
    public struct PartitionKey
    {
        private static readonly PartitionKeyInternal NullPartitionKeyInternal = new Microsoft.Azure.Documents.PartitionKey(null).InternalKey;
        private static readonly PartitionKeyInternal TruePartitionKeyInternal = new Microsoft.Azure.Documents.PartitionKey(true).InternalKey;
        private static readonly PartitionKeyInternal FalsePartitionKeyInternal = new Microsoft.Azure.Documents.PartitionKey(false).InternalKey;

        /// <summary>
        /// The returned object represents a partition key value that allows creating and accessing items
        /// without a value for partition key.
        /// </summary>
        public static readonly PartitionKey None = new PartitionKey(Microsoft.Azure.Documents.PartitionKey.None.InternalKey, true);

        /// <summary>
        /// The returned object represents a partition key value that allows creating and accessing items
        /// with a null value for the partition key.
        /// </summary>
        public static readonly PartitionKey Null = new PartitionKey(PartitionKey.NullPartitionKeyInternal);

        /// <summary>
        /// The tag name to use in the documents for specifying a partition key value
        /// when inserting such documents into a migrated collection
        /// </summary>
        public static readonly string SystemKeyName = Microsoft.Azure.Documents.PartitionKey.SystemKeyName;

        /// <summary>
        /// The partition key path in the collection definition for migrated collections
        /// </summary>
        public static readonly string SystemKeyPath = Microsoft.Azure.Documents.PartitionKey.SystemKeyPath;

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
                this.InternalKey = new Microsoft.Azure.Documents.PartitionKey(partitionKeyValue).InternalKey;
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
            this.InternalKey = new Microsoft.Azure.Documents.PartitionKey(partitionKeyValue).InternalKey;
            this.IsNone = false;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="value">The value to use as partition key.</param>
        internal PartitionKey(object value)
        {
            this.InternalKey = new Microsoft.Azure.Documents.PartitionKey(value).InternalKey;
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
    }
}
