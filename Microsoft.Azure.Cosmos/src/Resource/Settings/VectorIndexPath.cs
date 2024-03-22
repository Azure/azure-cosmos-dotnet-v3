//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Defines the target data type of an vector index path specification in the Azure Cosmos DB service.
    /// </summary>
    public enum VectorIndexType
    {
        /// <summary>
        /// Represents a flat vector index type.
        /// </summary>
        [EnumMember(Value = "Flat")]
        Flat,

        /// <summary>
        /// Represents a Disk ANN vector index type.
        /// </summary>
        [EnumMember(Value = "DiskANN")]
        DiskANN,

        /// <summary>
        /// Represents a quantized flat vector index type.
        /// </summary>
        [EnumMember(Value = "QuantizedFlat")]
        QuantizedFlat
    }

    /// <summary>
    /// DOM for a vector index path.
    /// A vector index path is used in a vector index.
    /// For example if you want to run a query like "SELECT * FROM c ORDER BY c.age, c.height",
    /// then you need to add "/age" and "/height" as composite paths to your composite index.
    /// </summary>
    public sealed class VectorIndexPath
    {
        /// <summary>
        /// Gets or sets the full path in a document used for composite indexing.
        /// We do not support wild cards in the path.
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.Path)]
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the sort order for the composite path.
        /// For example if you want to run the query "SELECT * FROM c ORDER BY c.age asc, c.height desc",
        /// then you need to make the order for "/age" "ascending" and the order for "/height" "descending".
        /// </summary>
        [JsonProperty(PropertyName = Constants.Properties.VectorIndexType)]
        [JsonConverter(typeof(StringEnumConverter))]
        public VectorIndexType Type { get; set; }

        /// <summary>
        /// This contains additional values for scenarios where the SDK is not aware of new fields. 
        /// This ensures that if resource is read and updated none of the fields will be lost in the process.
        /// </summary>
        [JsonExtensionData]
        internal IDictionary<string, JToken> AdditionalProperties { get; private set; }
    }
}