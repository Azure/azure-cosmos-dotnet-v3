//-----------------------------------------------------------------------
// <copyright file="JsonNewtonsoftCosmosDBBinaryWriter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// A class that implements IJsonWriter that really is just a wrapper around our Newtonsoft wrapper for binary.
    /// </summary>
    internal sealed class JsonNewtonsoftCosmosDBBinaryWriter : JsonNewtonsoftWriter
    {
        /// <summary>
        /// Initializes a new instance of the JsonNewtonsoftCosmosDBBinaryWriter class.
        /// </summary>
        public JsonNewtonsoftCosmosDBBinaryWriter()
        {
            this.writer = new JsonCosmosDBWriter(JsonSerializationFormat.Binary);
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

        /// <summary>
        /// Gets the result of writing all the tokens.
        /// </summary>
        /// <returns>The result of writing all the tokens.</returns>
        public override byte[] GetResult()
        {
            return ((Microsoft.Azure.Cosmos.Json.JsonCosmosDBWriter)this.writer).GetResult();
        }
    }
}
