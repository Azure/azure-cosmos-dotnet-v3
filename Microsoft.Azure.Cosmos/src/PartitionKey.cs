//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Represents a partition key value in the Azure Cosmos DB service.
    /// </summary>
    public sealed class PartitionKey
    {
        /// <summary>
        /// The returned object represents a partition key value that allows creating and accessing documents
        /// without a value for partition key.
        /// </summary>
        public static readonly PartitionKey NonePartitionKeyValue = new PartitionKey(Documents.PartitionKey.None);

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
        internal object Value { get; }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(string partitionKeyValue)
        {
            if (partitionKeyValue == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyValue));
            }

            this.Value = partitionKeyValue;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(bool partitionKeyValue)
        {
            this.Value = partitionKeyValue;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(double partitionKeyValue)
        {
            this.Value = partitionKeyValue;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(Guid partitionKeyValue)
        {
            this.Value = partitionKeyValue.ToString();
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(object partitionKeyValue)
        {
            if (partitionKeyValue == null)
            {
                throw new ArgumentNullException(nameof(partitionKeyValue));
            }

            this.Value = partitionKeyValue;
        }

        /// <summary>
        /// Gets the string representation of the partition key value.
        /// </summary>
        /// <returns>The string representation of the partition key value</returns>
        public new string ToString()
        {
            if (this.Value is Documents.PartitionKey)
            {
                return ((Documents.PartitionKey)this.Value).InternalKey.ToJsonString();
            }

            if (this.Value is Cosmos.PartitionKey)
            {
                return ((Cosmos.PartitionKey)this.Value).ToString();
            }

            return new Documents.PartitionKey(this.Value).InternalKey.ToJsonString();
        } 
    }
}
