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
    public sealed class PartitionKeyValueList
    {
        private readonly IList<object> partitionKeyValues;

        /// <summary>
        /// Creates a new partition key value list object.
        /// </summary>
        public PartitionKeyValueList()
        {
            this.partitionKeyValues = new List<object>();
        }

        /// <summary>
        /// Adds a partition key value of type string to the list.
        /// </summary>
        /// <param name="val">The value of type string to be used as partitionKey.</param>
        public void Add(string val)
        {
            if (val == null)
            {
                this.AddUndefined();
            }
            else
            {
                this.partitionKeyValues.Add(val);
            }
        }

        /// <summary>
        /// Adds a partition key value of type double to the list.
        /// </summary>
        /// <param name="val">The value of type double to be used as partitionKey.</param>
        public void Add(double val)
        {
            this.partitionKeyValues.Add(val);
        }

        /// <summary>
        /// Adds a partition key value of type bool to the list.
        /// </summary>
        /// <param name="val">The value of type bool to be used as partitionKey.</param>
        public void Add(bool val)
        {
            this.partitionKeyValues.Add(val);
        }

        /// <summary>
        /// Adds an Undefined partition key to the list.
        /// </summary>
        public void AddUndefined()
        {
            this.partitionKeyValues.Add(Undefined.Value);
        }

        internal IReadOnlyCollection<object> partitionKeyObjects => (IReadOnlyCollection<object>)this.partitionKeyValues;
    }
}