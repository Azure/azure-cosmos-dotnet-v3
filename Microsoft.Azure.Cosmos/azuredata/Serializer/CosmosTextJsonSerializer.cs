//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Buffers;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using Azure.Cosmos.Serialization;

    /// <summary>
    /// The default Cosmos JSON.NET serializer.
    /// </summary>
    internal sealed class CosmosTextJsonSerializer : CosmosSerializer
    {
        private static ReadOnlySpan<byte> Utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };
        private const int UnseekableStreamInitialRentSize = 4096;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        /// <summary>
        /// Creates a Serializer using System.Text.Json for user type handling.
        /// </summary>
        internal static CosmosTextJsonSerializer CreateUserDefaultSerializer(CosmosSerializationOptions cosmosSerializerOptions = null)
        {
            JsonSerializerOptions jsonSerializerOptions;
            if (cosmosSerializerOptions != null)
            {
                jsonSerializerOptions = new JsonSerializerOptions()
                {
                    IgnoreNullValues = cosmosSerializerOptions.IgnoreNullValues,
                    WriteIndented = cosmosSerializerOptions.Indented,
                    PropertyNamingPolicy = cosmosSerializerOptions.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase ?
                            JsonNamingPolicy.CamelCase : null
                };
            }
            else
            {
                jsonSerializerOptions = new JsonSerializerOptions();
            }

            CosmosTextJsonSerializer.InitializeDataContractConverters(jsonSerializerOptions);
            return new CosmosTextJsonSerializer(jsonSerializerOptions);
        }

        /// <summary>
        /// Creates a Serializer using System.Text.Json for REST type handling.
        /// </summary>
        internal static CosmosTextJsonSerializer CreatePropertiesSerializer()
        {
            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions();
            CosmosTextJsonSerializer.InitializeDataContractConverters(jsonSerializerOptions);
            CosmosTextJsonSerializer.InitializeRESTConverters(jsonSerializerOptions);
            return new CosmosTextJsonSerializer(jsonSerializerOptions);
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
            this.jsonSerializerOptions = jsonSerializerSettings ?? throw new ArgumentNullException(nameof(jsonSerializerSettings));
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
                if (stream.CanSeek
                    && stream.Length == 0)
                {
                    return default(T);
                }

                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return (T)(object)stream;
                }

                return CosmosTextJsonSerializer.Deserialize<T>(stream, this.jsonSerializerOptions);
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
            using Utf8JsonWriter utf8JsonWriter = new Utf8JsonWriter(streamPayload, new JsonWriterOptions() { Indented = this.jsonSerializerOptions.WriteIndented });
            JsonSerializer.Serialize<T>(utf8JsonWriter, input, this.jsonSerializerOptions);
            streamPayload.Position = 0;
            return streamPayload;
        }

        /// <summary>
        /// Throughput and allocations optimized deserialization.
        /// </summary>
        /// <remarks>Based off JsonDocument.ReadToEnd https://github.com/dotnet/runtime/blob/master/src/libraries/System.Text.Json/src/System/Text/Json/Document/JsonDocument.Parse.cs#L577. </remarks>
        internal static T Deserialize<T>(
            Stream stream,
            JsonSerializerOptions jsonSerializerOptions)
        {
            MemoryStream memoryStream = stream as MemoryStream;
            if (memoryStream != null
                && memoryStream.TryGetBuffer(out ArraySegment<byte> buffer))
            {
                if (buffer.Count >= CosmosTextJsonSerializer.Utf8Bom.Length
                    && CosmosTextJsonSerializer.Utf8Bom.SequenceEqual(buffer.AsSpan(0, CosmosTextJsonSerializer.Utf8Bom.Length)))
                {
                    // Skip 3 BOM bytes
                    return JsonSerializer.Deserialize<T>(buffer.AsSpan(CosmosTextJsonSerializer.Utf8Bom.Length), jsonSerializerOptions);
                }

                return JsonSerializer.Deserialize<T>(buffer, jsonSerializerOptions);
            }

            int written = 0;
            byte[] rented = null;

            ReadOnlySpan<byte> utf8Bom = CosmosTextJsonSerializer.Utf8Bom;

            try
            {
                if (stream.CanSeek)
                {
                    // Ask for 1 more than the length to avoid resizing later,
                    // which is unnecessary in the common case where the stream length doesn't change.
                    long expectedLength = Math.Max(utf8Bom.Length, stream.Length - stream.Position) + 1;
                    rented = ArrayPool<byte>.Shared.Rent(checked((int)expectedLength));
                }
                else
                {
                    rented = ArrayPool<byte>.Shared.Rent(CosmosTextJsonSerializer.UnseekableStreamInitialRentSize);
                }

                int lastRead;

                // Read up to 3 bytes to see if it's the UTF-8 BOM
                do
                {
                    // No need for checking for growth, the minimal rent sizes both guarantee it'll fit.
                    Debug.Assert(rented.Length >= utf8Bom.Length);

                    lastRead = stream.Read(
                        rented,
                        written,
                        utf8Bom.Length - written);

                    written += lastRead;
                }
                while (lastRead > 0 && written < utf8Bom.Length);

                // If we have 3 bytes, and they're the BOM, reset the write position to 0.
                if (written == utf8Bom.Length &&
                    utf8Bom.SequenceEqual(rented.AsSpan(0, utf8Bom.Length)))
                {
                    written = 0;
                }

                do
                {
                    if (rented.Length == written)
                    {
                        byte[] toReturn = rented;
                        rented = ArrayPool<byte>.Shared.Rent(checked(toReturn.Length * 2));
                        Buffer.BlockCopy(toReturn, 0, rented, 0, toReturn.Length);
                        // Holds document content, clear it.
                        ArrayPool<byte>.Shared.Return(toReturn, clearArray: true);
                    }

                    lastRead = stream.Read(rented, written, rented.Length - written);
                    written += lastRead;
                }
                while (lastRead > 0);

                return JsonSerializer.Deserialize<T>(rented.AsSpan(0, written), jsonSerializerOptions);
            }
            finally
            {
                if (rented != null)
                {
                    // Holds document content, clear it before returning it.
                    rented.AsSpan(0, written).Clear();
                    ArrayPool<byte>.Shared.Return(rented);
                }
            }
        }

        /// <summary>
        /// System.Text.Json does not support DataContract, so all DataContract types require custom converters, and all Direct classes too.
        /// </summary>
        public static void InitializeDataContractConverters(JsonSerializerOptions serializerOptions)
        {
            serializerOptions.Converters.Add(new TextJsonCosmosSqlQuerySpecConverter());
        }

        public static void InitializeRESTConverters(JsonSerializerOptions serializerOptions)
        {
            serializerOptions.Converters.Add(new TextJsonOfferV2Converter());
            serializerOptions.Converters.Add(new TextJsonAccountConsistencyConverter());
            serializerOptions.Converters.Add(new TextJsonAccountPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonAccountRegionConverter());
            serializerOptions.Converters.Add(new TextJsonCompositePathConverter());
            serializerOptions.Converters.Add(new TextJsonConflictPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonConflictResolutionPolicyConverter());
            serializerOptions.Converters.Add(new TextJsonContainerPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonDatabasePropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonExcludedPathConverter());
            serializerOptions.Converters.Add(new TextJsonIncludedPathConverter());
            serializerOptions.Converters.Add(new TextJsonIndexConverter());
            serializerOptions.Converters.Add(new TextJsonIndexingPolicyConverter());
            serializerOptions.Converters.Add(new TextJsonPermissionPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonSpatialPathConverter());
            serializerOptions.Converters.Add(new TextJsonStoredProcedurePropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonThroughputPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonTriggerPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonUniqueKeyConverter());
            serializerOptions.Converters.Add(new TextJsonUniqueKeyPolicyConverter());
            serializerOptions.Converters.Add(new TextJsonUserDefinedFunctionPropertiesConverter());
            serializerOptions.Converters.Add(new TextJsonUserPropertiesConverter());
        }
    }
}
