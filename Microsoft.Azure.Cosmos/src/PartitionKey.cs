//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;

    /// <summary>
    /// Represents a partition key value in the Azure Cosmos DB service.
    /// </summary>
    public class PartitionKey
    {
        private readonly object partitionKeyValue;
        private Lazy<string> partitionKeyValueAsString;

        /// <summary>
        /// The returned object represents a partition key value that allows creating and accessing documents
        /// without a value for partition key.
        /// </summary>
        public static readonly PartitionKey NonePartitionKeyValue = new PartitionKey(Microsoft.Azure.Documents.PartitionKey.None);

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
        public virtual object Value => this.partitionKeyValue;

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <remarks>Usable for mocking scenarios.</remarks>
        protected PartitionKey()
            : this(Documents.Undefined.Value)
        {
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

            this.partitionKeyValue = partitionKeyValue;
            this.partitionKeyValueAsString = new Lazy<string>(this.GetPartitionKeyValueAsString);
        }

        /// <summary>
        /// Gets the string representation of the partition key value.
        /// </summary>
        /// <returns>The string representation of the partition key value</returns>
        public new virtual string ToString() => this.partitionKeyValueAsString.Value;

        private string GetPartitionKeyValueAsString()
        {
            if (this.partitionKeyValue is Documents.PartitionKey)
            {
                return ((Documents.PartitionKey)this.partitionKeyValue).InternalKey.ToJsonString();
            }

            return new Documents.PartitionKey(this.partitionKeyValue).InternalKey.ToJsonString();
        } 
    }
}
