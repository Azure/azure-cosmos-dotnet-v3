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
    public enum CompositePathSortOrder
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
    public sealed class CompositePathDefinition 
    {
        /// <summary>
        /// Creates a new instance of CompositePathDefinition with given path and sort-order
        /// </summary>
        public static CompositePathDefinition Create(string path, CompositePathSortOrder sortOrder)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException($"{nameof(path)}");
            }

            return new CompositePathDefinition()
            {
                Path = path,
                Order = sortOrder,
            };
        }

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
        public CompositePathSortOrder Order { get; set; }
    }
}