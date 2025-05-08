//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Defines the distance function for a vector index specification in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Embedding"/> for usage.
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DistanceFunction
    {
        /// <summary>
        /// Represents default value.
        /// </summary>
        None,

        /// <summary>
        /// Represents the euclidean distance function.
        /// </summary>
        [EnumMember(Value = "euclidean")]
        Euclidean,

        /// <summary>
        /// Represents the cosine distance function.
        /// </summary>
        [EnumMember(Value = "cosine")]
        Cosine,

        /// <summary>
        /// Represents the dot product distance function.
        /// </summary>
        [EnumMember(Value = "dotproduct")]
        DotProduct
    }
}
