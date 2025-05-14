//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Defines the distance function for a vector index specification in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Embedding"/> for usage.
    public enum DistanceFunction
    {
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
