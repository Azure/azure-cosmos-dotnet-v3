//-----------------------------------------------------------------------
// <copyright file="JsonNewtonsoftNewtonsoftTextReader.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.NetFramework.Tests.Json
{
    using System.IO;
    using Microsoft.Azure.Cosmos.Json;
    
    /// <summary>
    /// Class that implements IJsonReader but is really just a wrapper around Newtonsoft's JsonTextReader.
    /// </summary>
    internal sealed class JsonNewtonsoftNewtonsoftTextReader : JsonNewtonsoftReader
    {
        /// <summary>
        /// Initializes a new instance of the JsonNewtonsoftNewtonsoftTextReader class.
        /// </summary>
        /// <param name="jsonText">The json text to read.</param>
        public JsonNewtonsoftNewtonsoftTextReader(string jsonText) 
            : base(new Newtonsoft.Json.JsonTextReader(new StringReader(jsonText)))
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
