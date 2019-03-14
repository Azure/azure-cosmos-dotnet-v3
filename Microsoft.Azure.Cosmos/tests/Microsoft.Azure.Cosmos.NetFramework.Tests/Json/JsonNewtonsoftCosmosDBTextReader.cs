//-----------------------------------------------------------------------
// <copyright file="JsonNewtonsoftCosmosDBTextReader.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Json;

    /// <summary>
    /// Class that implements IJsonReader that really is just a wrapper around our newtonsoft wrapper.
    /// </summary>
    internal sealed class JsonNewtonsoftCosmosDBTextReader : JsonNewtonsoftReader
    {
        /// <summary>
        /// Initializes a new instance of the JsonNewtonsoftCosmosDBTextReader class.
        /// </summary>
        /// <param name="jsonText">The JSON text.</param>
        public JsonNewtonsoftCosmosDBTextReader(string jsonText) 
            : base(new JsonCosmosDBReader(new MemoryStream(Encoding.UTF8.GetBytes(jsonText))))
        {
        }

        /// <summary>
        /// Gets the JsonSerializationFormat.
        /// </summary>
        public override JsonSerializationFormat SerializationFormat
        {
            get
            {
                return JsonSerializationFormat.Text;
            }
        }
    }
}
