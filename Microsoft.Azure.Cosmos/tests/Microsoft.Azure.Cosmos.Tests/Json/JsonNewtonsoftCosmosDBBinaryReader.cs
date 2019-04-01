//-----------------------------------------------------------------------
// <copyright file="JsonNewtonsoftCosmosDBBinaryReader.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System.IO;
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// Wrapper class that implements an IJsonReader that really is a wrapper around our Newtonsoft wrapper for binary.
    /// </summary>
    internal sealed class JsonNewtonsoftCosmosDBBinaryReader : JsonNewtonsoftReader
    {
        /// <summary>
        /// Initializes a new instance of the JsonNewtonsoftCosmosDBBinaryReader class.
        /// </summary>
        /// <param name="jsonText">The json text.</param>
        public JsonNewtonsoftCosmosDBBinaryReader(string jsonText) 
            : base(new JsonCosmosDBReader(new MemoryStream(JsonPerfMeasurement.ConvertTextToBinary(jsonText))))
        {
        }

        /// <summary>
        /// Gets the JsonSerializationFormat.
        /// </summary>
        public override JsonSerializationFormat SerializationFormat
        {
            get
            {
                return JsonSerializationFormat.Binary;
            }
        }
    }
}
