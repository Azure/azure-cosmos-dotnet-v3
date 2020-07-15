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
        internal readonly IList<object> PartitionKeyValues;

        /// <summary>
        /// Creates a new partition key value list object.
        /// </summary>
        public PartitionKeyValueList()
        {
            this.PartitionKeyValues = new List<object>();
        }

        /// <summary>
        /// Adds a partition key value of type string to the list.
        /// </summary>
        /// <param name="val">The value of type string to be used as partitionKey.</param>
        public void Add(string val)
        {
            if (val == null)
            {
                this.AddNullValue();
            }
            else
            {
                this.PartitionKeyValues.Add(val);
            }
        }

        /// <summary>
        /// Adds a partition key value of type double to the list.
        /// </summary>
        /// <param name="val">The value of type double to be used as partitionKey.</param>
        public void Add(double val)
        {
            this.PartitionKeyValues.Add(val);
        }

        /// <summary>
        /// Adds a partition key value of type bool to the list.
        /// </summary>
        /// <param name="val">The value of type bool to be used as partitionKey.</param>
        public void Add(bool val)
        {
            this.PartitionKeyValues.Add(val);
        }

        /// <summary>
        /// Adds a partition key value which is null
        /// </summary>
        public void AddNullValue()
        {
            this.PartitionKeyValues.Add(null);
        }

        /// <summary>
        /// Adds a None partition key value.
        /// </summary>
        public void AddNoneType()
        {
            this.PartitionKeyValues.Add(PartitionKey.None);
        }
    }
}