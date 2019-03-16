//-----------------------------------------------------------------------
// <copyright file="CompositePath.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Defines the target data type of an index path specification in the Azure Cosmos DB service.
    /// </summary>
    internal enum CompositePathSortOrder
    {
        /// <summary>
        /// Ascending sort order for composite paths.
        /// </summary>
        [EnumMember(Value = "ascending")]
        Ascending,

        /// <summary>
        /// Descending sort order for composite paths.
        /// </summary>
        [EnumMember(Value = "descending")]
        Descending
    }

    /// <summary>
    /// DOM for a composite path.
    /// A composite path is used in a composite index.
    /// For example if you want to run a query like "SELECT * FROM c ORDER BY c.age, c.height",
    /// then you need to add "/age" and "/height" as composite paths to your composite index.
    /// </summary>
    internal sealed class CompositePath 
    {
        /// <summary>
        /// Gets or sets the full path in a document used for composite indexing.
        /// We do not support wildcards in the path.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the sort order for the composite path.
        /// For example if you want to run the query "SELECT * FROM c ORDER BY c.age asc, c.height desc",
        /// then you need to make the order for "/age" "ascending" and the order for "/height" "descending".
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Order)]
        [JsonConverter(typeof(StringEnumConverter))]
        public CompositePathSortOrder Order
        {
            get
            {
                CompositePathSortOrder result = default(CompositePathSortOrder);
                string sortOrder = this.GetValue<string>(Constants.Properties.Order);
                if (!string.IsNullOrEmpty(sortOrder))
                {
                    result = (CompositePathSortOrder)Enum.Parse(typeof(CompositePathSortOrder), sortOrder, true);
                }

                return result;
            }

            set
            {
                this.SetValue(Constants.Properties.Order, value);
            }
        }
    }
}