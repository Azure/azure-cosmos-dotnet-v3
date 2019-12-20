//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text.Json;
    using Azure.Cosmos.Serialization;

    /// <summary>
    /// The default Cosmos JSON.NET serializer.
    /// </summary>
    internal sealed class CosmosTextJsonSerializer : CosmosSerializer
    {
        private static JsonSerializerOptions DefaultSerializationOptions = new JsonSerializerOptions() { WriteIndented = false, DefaultBufferSize = 1024 };
        private readonly JsonSerializerOptions jsonSerializerSettings;

        /// <summary>
        /// Create a serializer that uses the JSON.net serializer
        /// </summary>
        /// <remarks>
        /// This is internal to reduce exposure of JSON.net types so
        /// it is easier to convert to System.Text.Json
        /// </remarks>
        internal CosmosTextJsonSerializer()
        {
            this.jsonSerializerSettings = new JsonSerializerOptions();
        }

        /// <summary>
        /// Create a serializer that uses the JSON.net serializer
        /// </summary>
        /// <remarks>
        /// This is internal to reduce exposure of JSON.net types so
        /// it is easier to convert to System.Text.Json
        /// </remarks>
        internal CosmosTextJsonSerializer(JsonSerializerOptions jsonSerializerSettings)
        {
            this.jsonSerializerSettings = jsonSerializerSettings;
        }

        /// <summary>
        /// Create a serializer that uses the JSON.net serializer
        /// </summary>
        /// <remarks>
        /// This is internal to reduce exposure of JSON.net types so
        /// it is easier to convert to System.Text.Json
        /// </remarks>
        internal CosmosTextJsonSerializer(CosmosSerializationOptions cosmosSerializerOptions)
        {
            this.jsonSerializerSettings = new JsonSerializerOptions()
            {
                IgnoreNullValues = cosmosSerializerOptions.IgnoreNullValues,
                WriteIndented = cosmosSerializerOptions.Indented,
                PropertyNamingPolicy = cosmosSerializerOptions.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase ?
                        JsonNamingPolicy.CamelCase : null
            };
        }

        /// <summary>
        /// Convert a Stream to the passed in type.
        /// </summary>
        /// <typeparam name="T">The type of object that should be deserialized</typeparam>
        /// <param name="stream">An open stream that is readable that contains JSON</param>
        /// <returns>The object representing the deserialized stream</returns>
        public override T FromStream<T>(Stream stream)
        {
            using (stream)
            {
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return (T)(object)stream;
                }

                ReadOnlySpan<byte> span;
                MemoryStream memoryStream = stream as MemoryStream;
                if (memoryStream != null)
                {
                    span = new ReadOnlySpan<byte>(memoryStream.ToArray());
                }
                else
                {
                    byte[] buffer = new byte[16 * 1024];
                    using (MemoryStream ms = new MemoryStream())
                    {
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }

                        span = new ReadOnlySpan<byte>(ms.ToArray());
                    }
                    
                }
                
                return JsonSerializer.Deserialize<T>(span, this.jsonSerializerSettings);
            }
        }

        /// <summary>
        /// Converts an object to a open readable stream
        /// </summary>
        /// <typeparam name="T">The type of object being serialized</typeparam>
        /// <param name="input">The object to be serialized</param>
        /// <returns>An open readable stream containing the JSON of the serialized object</returns>
        public override Stream ToStream<T>(T input)
        {
            MemoryStream streamPayload = new MemoryStream();
            using Utf8JsonWriter utf8JsonWriter = new Utf8JsonWriter(streamPayload);
            JsonSerializer.Serialize<T>(utf8JsonWriter, input, CosmosTextJsonSerializer.DefaultSerializationOptions);
            streamPayload.Position = 0;
            return streamPayload;
        }
    }
}
