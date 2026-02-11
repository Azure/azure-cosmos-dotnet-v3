//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json;
    using System.Runtime.Serialization;

    /// <summary>
    /// Defines the target index type of an vector index path specification in the Azure Cosmos DB service.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum QuantizerType
    {
        /// <summary>
        /// Represents Product Quantizer
        /// </summary>
        [EnumMember(Value = "product")]
        Product,

        /// <summary>
        /// Represents Spherical Quantizer
        /// </summary>
        [EnumMember(Value = "spherical")]
        Spherical,
    }
}
