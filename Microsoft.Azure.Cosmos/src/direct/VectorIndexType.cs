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
    internal enum VectorIndexType
    {
        /// <summary>
        /// Represents a flat vector index type.
        /// </summary>
        [EnumMember(Value = "flat")]
        Flat,

        /// <summary>
        /// Represents a Disk ANN vector index type.
        /// </summary>
        [EnumMember(Value = "diskANN")]
        DiskANN,

        /// <summary>
        /// Represents a quantized flat vector index type.
        /// </summary>
        [EnumMember(Value = "quantizedFlat")]
        QuantizedFlat
    }
}
