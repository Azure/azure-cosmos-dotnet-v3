//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos
{
    using System.Runtime.Serialization;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Defines the target data type of a vector index specification in the Azure Cosmos DB service.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum VectorDataType
    {
        /// <summary>
        /// Represents default value.
        /// </summary>
        None,

        /// <summary>
        /// Represent a float32 data type.
        /// </summary>
        [EnumMember(Value = "float32")]
        Float32,

        /// <summary>
        /// Represent an uint8 data type.
        /// </summary>
        [EnumMember(Value = "uint8")]
        Uint8,

        /// <summary>
        /// Represent a int8 data type.
        /// </summary>
        [EnumMember(Value = "int8")]
        Int8
    }
}
