//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json;
    using System.Runtime.Serialization;

    /// <summary>
    /// Defines the distance function for a vector index specification in the Azure Cosmos DB service.
    /// </summary>
    /// <seealso cref="Embedding"/> for usage.
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum DistanceFunction
    {
        /// <summary>
        /// Represents the euclidean distance function.
        /// </summary>
        [EnumMember(Value = Constants.Properties.Euclidean)]
        Euclidean,

        /// <summary>
        /// Represents the cosine distance function.
        /// </summary>
        [EnumMember(Value = Constants.Properties.Cosine)]
        Cosine,

        /// <summary>
        /// Represents the dot product distance function.
        /// </summary>
        [EnumMember(Value = Constants.Properties.DotProduct)]
        DotProduct
    }
}
