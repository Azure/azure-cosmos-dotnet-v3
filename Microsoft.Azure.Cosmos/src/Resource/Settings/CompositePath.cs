//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

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
    public sealed class CompositePath
    {
        /// <summary>
        /// Gets or sets the full path in a document used for composite indexing.
        /// We do not support wild cards in the path.
        /// </summary>
        [JsonPropertyName(Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the sort order for the composite path.
        /// For example if you want to run the query "SELECT * FROM c ORDER BY c.age asc, c.height desc",
        /// then you need to make the order for "/age" "ascending" and the order for "/height" "descending".
        /// </summary>
        [JsonPropertyName(Constants.Properties.Order)]
        [JsonConverter(typeof(JsonStringEnumConverter<CompositePathSortOrder>))]
        public CompositePathSortOrder Order { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JsonElement> AdditionalProperties { get; private set; }
    }
}