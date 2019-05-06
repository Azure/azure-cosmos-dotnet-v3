//-----------------------------------------------------------------------
// <copyright file="CosmosElement.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.CosmosElements
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Json;

    internal static class CosmosElementSerializer
    {
        /// <summary>
        /// Converts a list of CosmosElements into a memory stream.
        /// </summary>
        /// <param name="cosmosElements">The cosmos elements</param>
        /// <param name="cosmosSerializationOptions">The custom serialization options. This allows custom serialization types like BSON, JSON, or other formats</param>
        /// <returns>Returns a memory stream of cosmos elements. By default the memory stream will contain JSON.</returns>
        internal static Stream ToStream(
            IEnumerable<CosmosElement> cosmosElements,
            CosmosSerializationOptions cosmosSerializationOptions = null)
        {
            IJsonWriter jsonWriter;
            if (cosmosSerializationOptions != null)
            {
                jsonWriter = cosmosSerializationOptions.CreateCustomWriterCallback();
            }
            else
            {
                jsonWriter = JsonWriter.Create(JsonSerializationFormat.Text);
            }

            jsonWriter.WriteArrayStart();

            foreach (CosmosElement cosmosElement in cosmosElements)
            {
                cosmosElement.WriteTo(jsonWriter);
            }

            jsonWriter.WriteArrayEnd();

            return new MemoryStream(jsonWriter.GetResult());
        }

        /// <summary>
        /// Converts a list of CosmosElements into a list of objects.
        /// </summary>
        /// <param name="cosmosElements">The cosmos elements</param>
        /// <param name="jsonSerializer">The JSON </param>
        /// <param name="cosmosSerializationOptions">The custom serialization options. This allows custom serialization types like BSON, JSON, or other formats</param>
        /// <returns>Returns a memory stream of cosmos elements. By default the memory stream will contain JSON.</returns>
        internal static IEnumerable<T> Deserialize<T>(
            IEnumerable<CosmosElement> cosmosElements,
            CosmosJsonSerializer jsonSerializer,
            CosmosSerializationOptions cosmosSerializationOptions = null)
        {
            if (!cosmosElements.Any())
            {
                return Enumerable.Empty<T>();
            }

            Stream stream = CosmosElementSerializer.ToStream(cosmosElements, cosmosSerializationOptions);
            IEnumerable<T> typedResults = jsonSerializer.FromStream<List<T>>(stream);

            return typedResults;
        }
    }
}
