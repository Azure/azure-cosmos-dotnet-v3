//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Represents a partition key value in the Azure Cosmos DB service.
    /// </summary>
    public class PartitionKey
    {
        private readonly object partitionKeyValue;
        private Lazy<string> partitionKeyValueAsString;

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
            return new Documents.PartitionKey(this.partitionKeyValue).InternalKey.ToJsonString();
        } 
    }
}
