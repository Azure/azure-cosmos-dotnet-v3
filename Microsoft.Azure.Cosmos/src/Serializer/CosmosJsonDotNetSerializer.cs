//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.IO;
    using System.Text;
    using Microsoft.Azure.Cosmos.Serializer;
    using Microsoft.Azure.Documents;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Serialization;

    /// <summary>
    /// The default Cosmos JSON.NET serializer.
    /// </summary>
    internal sealed class CosmosJsonDotNetSerializer : CosmosSerializer
    {
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
        private readonly JsonSerializerSettings SerializerSettings;
        private readonly bool BinaryEncodingEnabled;

        /// <summary>
        /// Create a serializer that uses the JSON.net serializer
        /// </summary>
        /// <remarks>
        /// This is internal to reduce exposure of JSON.net types so
        /// it is easier to convert to System.Text.Json
        /// </remarks>
        internal CosmosJsonDotNetSerializer(
            bool binaryEncodingEnabled = false)
        {
            this.SerializerSettings = null;
            this.BinaryEncodingEnabled = binaryEncodingEnabled;
        }

        /// <summary>
        /// Create a serializer that uses the JSON.net serializer
        /// </summary>
        /// <remarks>
        /// This is internal to reduce exposure of JSON.net types so
        /// it is easier to convert to System.Text.Json
        /// </remarks>
        internal CosmosJsonDotNetSerializer(
            CosmosSerializationOptions cosmosSerializerOptions,
            bool binaryEncodingEnabled = false)
        {
            if (cosmosSerializerOptions == null)
            {
                this.SerializerSettings = null;
                return;
            }

            JsonSerializerSettings jsonSerializerSettings = new JsonSerializerSettings()
            {
                NullValueHandling = cosmosSerializerOptions.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include,
                Formatting = cosmosSerializerOptions.Indented ? Formatting.Indented : Formatting.None,
                ContractResolver = cosmosSerializerOptions.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase
                    ? new CamelCasePropertyNamesContractResolver()
                    : null,
                MaxDepth = 64, // https://github.com/advisories/GHSA-5crp-9r3c-p9vr
            };

            this.SerializerSettings = jsonSerializerSettings;
            this.BinaryEncodingEnabled = binaryEncodingEnabled;
        }

        /// <summary>
        /// Create a serializer that uses the JSON.net serializer
        /// </summary>
        /// <remarks>
        /// This is internal to reduce exposure of JSON.net types so
        /// it is easier to convert to System.Text.Json
        /// </remarks>
        internal CosmosJsonDotNetSerializer(
            JsonSerializerSettings jsonSerializerSettings,
            bool binaryEncodingEnabled = false)
        {
            this.SerializerSettings = jsonSerializerSettings ?? throw new ArgumentNullException(nameof(jsonSerializerSettings));
            this.BinaryEncodingEnabled = binaryEncodingEnabled;
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

                JsonSerializer jsonSerializer = this.GetSerializer();

                if (stream is CloneableStream cloneableStream)
                {
                    using (CosmosBufferedStreamWrapper bufferedStream = new (cloneableStream, shouldDisposeInnerStream: false))
                    {
                        if (bufferedStream.GetJsonSerializationFormat() == Json.JsonSerializationFormat.Binary)
                        {
                            byte[] content = bufferedStream.ReadAll();

                            using Json.Interop.CosmosDBToNewtonsoftReader reader = new (
                                jsonReader: Json.JsonReader.Create(
                                    jsonSerializationFormat: Json.JsonSerializationFormat.Binary,
                                    buffer: content));

                            return jsonSerializer.Deserialize<T>(reader);
                        }
                    }
                }

                using (StreamReader sr = new (stream))
                {
                    using (JsonTextReader jsonTextReader = new (sr))
                    {
                        return jsonSerializer.Deserialize<T>(jsonTextReader);
                    }
                }
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
            MemoryStream streamPayload;
            JsonSerializer jsonSerializer = this.GetSerializer();

            // Binary encoding is currently not supported for internal types, for e.g.
            // container creation, database creation requests etc.
            if (this.BinaryEncodingEnabled
                && !CosmosSerializerCore.IsInputTypeInternal(typeof(T)))
            {
                using (Json.Interop.CosmosDBToNewtonsoftWriter writer = new (
                    jsonSerializationFormat: Json.JsonSerializationFormat.Binary))
                {
                    writer.Formatting = Formatting.None;
                    jsonSerializer.Serialize(writer, input);
                    streamPayload = new MemoryStream(writer.GetResult().ToArray());
                }
            }
            else
            {
                streamPayload = new ();
                using (StreamWriter streamWriter = new (streamPayload, encoding: CosmosJsonDotNetSerializer.DefaultEncoding, bufferSize: 1024, leaveOpen: true))
                {
                    using (JsonWriter writer = new JsonTextWriter(streamWriter))
                    {
                        writer.Formatting = Formatting.None;
                        jsonSerializer.Serialize(writer, input);
                        writer.Flush();
                        streamWriter.Flush();
                    }
                }
            }

            streamPayload.Position = 0;
            return streamPayload;
        }

        /// <summary>
        /// JsonSerializer has hit a race conditions with custom settings that cause null reference exception.
        /// To avoid the race condition a new JsonSerializer is created for each call
        /// </summary>
        private JsonSerializer GetSerializer()
        {
            return JsonSerializer.Create(this.SerializerSettings);
        }
    }
}
