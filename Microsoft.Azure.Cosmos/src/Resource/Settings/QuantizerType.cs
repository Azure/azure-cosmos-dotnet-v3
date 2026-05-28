//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Converters;

    /// <summary>
    /// Defines the quantizer type of a vector index path specification in the Azure Cosmos DB service.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
#if PREVIEW
    public
#else
    internal
#endif
    enum QuantizerType
    {
        /// <summary>
        /// Represents a product quantizer type.
        /// </summary>
        [EnumMember(Value = "product")]
        Product,

        /// <summary>
        /// Represents a spherical quantizer type.
        /// </summary>
        [EnumMember(Value = "spherical")]
        Spherical
    }
}