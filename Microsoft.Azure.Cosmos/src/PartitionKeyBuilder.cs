//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    /// <summary>
    /// Represents a partition key value list in the Azure Cosmos DB service.
    /// </summary>
#if SUBPARTITIONING
    public
#else
    internal
#endif
    sealed class PartitionKeyBuilder
    {
        private readonly IList<object> partitionKeyValues;

        private bool isBuilt = false;

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
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a PartitionKey. Create a new instance to build another.");
            }

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
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a PartitionKey. Create a new instance to build another.");
            }

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
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a PartitionKey. Create a new instance to build another.");
            }

            this.partitionKeyValues.Add(val);
            return this;
        }

        /// <summary>
        /// Adds a partition key value which is null
        /// </summary>
        /// <returns>An instance of <see cref="PartitionKeyBuilder"/> to use. </returns>
        public PartitionKeyBuilder AddNullValue()
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a PartitionKey. Create a new instance to build another.");
            }

            this.partitionKeyValues.Add(null);
            return this;
        }

        /// <summary>
        /// Adds a None partition key value.
        /// </summary>
        /// <returns>An instance of <see cref="PartitionKeyBuilder"/> to use. </returns>
        public PartitionKeyBuilder AddNoneType()
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a PartitionKey. Create a new instance to build another.");
            }

            this.partitionKeyValues.Add(PartitionKey.None);
            return this;
        }

        /// <summary>
        /// Builds a new instance of the <see cref="PartitionKey"/> with the specified Partition Key values.
        /// </summary>
        /// <returns>An instance of <see cref="PartitionKey"/> </returns>
        public PartitionKey Build()
        {
            if (this.isBuilt)
            {
                throw new InvalidOperationException("This builder instance has already been used to build a PartitionKey. Create a new instance to build another.");
            }

            if (this.partitionKeyValues.Count == 0
                || (this.partitionKeyValues.Count == 1 && PartitionKey.None.Equals(this.partitionKeyValues[0])))
            {
                this.isBuilt = true;
                return PartitionKey.None;
            }
            else
            {
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
                this.isBuilt = true;
                return new PartitionKey(partitionKeyInternal);
            }
        }
    }
}