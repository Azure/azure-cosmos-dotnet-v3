//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

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
        /// The returned object represents a partition key value that allows creating and accessing documents
        /// without a value for partition key.
        /// </summary>
        public static readonly PartitionKey NullPartitionKeyValue = new PartitionKey(new Documents.PartitionKey(null));

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
        internal PartitionKeyInternal Value { get; }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(string partitionKeyValue)
        {
            if (partitionKeyValue == null)
            {
                throw new ArgumentNullException($"{nameof(partitionKeyValue)} is null. Please use {nameof(PartitionKey.NullPartitionKeyValue)} to define null");
            }

            this.Value = new Documents.PartitionKey(partitionKeyValue).InternalKey;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(bool partitionKeyValue)
        {
            this.Value = new Documents.PartitionKey(partitionKeyValue).InternalKey;
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(double partitionKeyValue)
        {
            this.Value = new Documents.PartitionKey(partitionKeyValue).InternalKey;
        }

        /// <summary>
        /// Convert a string to a partition key
        /// </summary>
        /// <param name="partitionKeyValue">The string value</param>
        public static implicit operator PartitionKey(string partitionKeyValue)
        {
            return new PartitionKey(partitionKeyValue);
        }

        /// <summary>
        /// Convert a double to a partition key
        /// </summary>
        /// <param name="partitionKeyValue">The double value</param>
        public static implicit operator PartitionKey(double partitionKeyValue)
        {
            return new PartitionKey(partitionKeyValue);
        }

        /// <summary>
        /// Convert a bool to a partition key
        /// </summary>
        /// <param name="partitionKeyValue">The bool value</param>
        public static implicit operator PartitionKey(bool partitionKeyValue)
        {
            return new PartitionKey(partitionKeyValue);
        }

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="value">The value to use as partition key.</param>
        internal PartitionKey(object value)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            Documents.PartitionKey docPk = value as Documents.PartitionKey;
            if (docPk != null)
            {
                this.Value = docPk.InternalKey;
                return;
            }

            this.Value = value as PartitionKeyInternal;
            if (this.Value != null)
            {
                return;
            }

            this.Value = new Documents.PartitionKey(value).InternalKey;
        }

        /// <summary>
        /// Gets the string representation of the partition key value.
        /// </summary>
        /// <returns>The string representation of the partition key value</returns>
        public new string ToString()
        {
            if (this.Value == null)
            {
                return null;
            }

            return this.Value.ToJsonString();
        } 
    }
}
