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
        /// Gets the value provided at initialization.
        /// </summary>
        public virtual object Value => this.partitionKeyValue;

        /// <summary>
        /// Creates a new partition key value.
        /// </summary>
        /// <param name="partitionKeyValue">The value to use as partition key.</param>
        public PartitionKey(object partitionKeyValue)
        {
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
