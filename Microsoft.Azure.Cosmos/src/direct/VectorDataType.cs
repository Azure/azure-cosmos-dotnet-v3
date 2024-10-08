//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Documents
{
    using Newtonsoft.Json.Converters;
    using Newtonsoft.Json;
    using System.Runtime.Serialization;

    /// <summary>
    /// Defines the target data type of a vector index specification in the Azure Cosmos DB service.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum VectorDataType
    { 
        /// <summary>
        /// Represent a float32 data type.
        /// </summary>
        [EnumMember(Value = Constants.Properties.Float32)]
        Float32,

        /// <summary>
        /// Represent an uint8 data type.
        /// </summary>
        [EnumMember(Value = Constants.Properties.Uint8)]
        Uint8,

        /// <summary>
        /// Represent a int8 data type.
        /// </summary>
        [EnumMember(Value = Constants.Properties.Int8)]
        Int8
    }
}
