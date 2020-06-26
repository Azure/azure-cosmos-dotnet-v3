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
    public class PartitionKeyValueList
    {
        private IList<object> partitionKeyValues;

        /// <summary>
        /// Creates a new partition key value list.
        /// </summary>
        public PartitionKeyValueList()
        {
            this.partitionKeyValues = new List<object>();
        }

        /// <summary>
        /// Sets a partition key value of type string.
        /// </summary>
        /// <param name="val">The value of type string to be used as partitionKey.</param>
        public void Add(string val)
        {
            if (val == null)
            {
                this.Add();
                return;
            }
            this.partitionKeyValues.Add(val);
        }

        /// <summary>
        /// Sets a partition key value of type double.
        /// </summary>
        /// <param name="val">The value of type double to be used as partitionKey.</param>
        public void Add(double val)
        {
            this.partitionKeyValues.Add(val);
        }

        /// <summary>
        /// Sets a partition key value of type bool.
        /// </summary>
        /// <param name="val">The value of type bool to be used as partitionKey.</param>
        public void Add(bool val)
        {
            this.partitionKeyValues.Add(val);
        }

        private void Add()
        {
            this.partitionKeyValues.Add(Undefined.Value);
        }

        internal IReadOnlyCollection<object> partitionKeyObjects => (IReadOnlyCollection<object>)this.partitionKeyValues;
    }
}