//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Represents a partition key value list in the Azure Cosmos DB service.
    /// </summary>
#if PREVIEW
    public
#else
    internal
#endif
    sealed class PartitionKeyBuilder
    {
        private readonly List<object> partitionKeyValues;

        /// <summary>
        /// Creates a new partition key value list object.
        /// </summary>
        public PartitionKeyBuilder()
        {
            this.partitionKeyValues = new List<object>();
        }

        /// <summary>
        /// Adds a partition key value of type string to the list.
        /// </summary>
        /// <param name="val">The value of type string to be used as partition key.</param>
        /// <returns>An instance of <see cref="PartitionKeyBuilder"/> to use. </returns>
        public PartitionKeyBuilder Add(string val)
        {
            this.partitionKeyValues.Add(val);
            return this;
        }

        /// <summary>
        /// Adds a partition key value of type double to the list.
        /// </summary>
        /// <param name="val">The value of type double to be used as partition key.</param>
        /// <returns>An instance of <see cref="PartitionKeyBuilder"/> to use. </returns>
        public PartitionKeyBuilder Add(double val)
        {
            this.partitionKeyValues.Add(val);
            return this;
        }

        /// <summary>
        /// Adds a partition key value of type bool to the list.
        /// </summary>
        /// <param name="val">The value of type bool to be used as partition key.</param>
        /// <returns>An instance of <see cref="PartitionKeyBuilder"/> to use. </returns>
        public PartitionKeyBuilder Add(bool val)
        {
            this.partitionKeyValues.Add(val);
            return this;
        }

        /// <summary>
        /// Adds a partition key value which is null
        /// </summary>
        /// <returns>An instance of <see cref="PartitionKeyBuilder"/> to use. </returns>
        public PartitionKeyBuilder AddNullValue()
        {
            this.partitionKeyValues.Add(null);
            return this;
        }

        /// <summary>
        /// Adds a None partition key value.
        /// </summary>
        /// <returns>An instance of <see cref="PartitionKeyBuilder"/> to use. </returns>
        public PartitionKeyBuilder AddNoneType()
        {
            this.partitionKeyValues.Add(PartitionKey.None);
            return this;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="PartitionKey"/> with the specified Partition Key values.
        /// </summary>
        /// <returns>An instance of <see cref="PartitionKey"/> </returns>
        public PartitionKey Build()
        {
            // Why these checks?
            // These changes are being added for SDK to support multiple paths in a partition key.
            //
            // Currently, when a resource does not specify a value for the PartitionKey,
            // we assign a temporary value `PartitionKey.None` and later discern whether
            // it is a PartitionKey.Undefined or PartitionKey.Empty based on the Collection Type.
            // We retain this behaviour for single path partition keys.
            //
            // For collections with multiple path keys, absence of a partition key values is
            // always treated as a PartitionKey.Undefined.
            if (this.partitionKeyValues.Count == 0)
            {
                throw new ArgumentException($"No partition key value has been specifed");
            }

            if (this.partitionKeyValues.Count == 1 && PartitionKey.None.Equals(this.partitionKeyValues[0]))
            {
                return PartitionKey.None;
            }

            PartitionKeyInternal partitionKeyInternal;
            object[] valueArray = new object[this.partitionKeyValues.Count];
            for (int i = 0; i < this.partitionKeyValues.Count; i++)
            {
                object val = this.partitionKeyValues[i];
                if (PartitionKey.None.Equals(val))
                {
                    valueArray[i] = Undefined.Value;
                }
                else
                {
                    valueArray[i] = val;
                }
            }

            partitionKeyInternal = new Documents.PartitionKey(valueArray).InternalKey;
            return new PartitionKey(partitionKeyInternal);
        }
    }
}
