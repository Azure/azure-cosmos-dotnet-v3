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
        /// Create a serializer that uses the JSON.net serializer
        /// </summary>
        /// <remarks>
        /// This is internal to reduce exposure of JSON.net types so
        /// it is easier to convert to System.Text.Json
        /// </remarks>
        internal CosmosTextJsonSerializer()
        {
            this.jsonSerializerOptions = new JsonSerializerOptions();
            this.InitializeConverters();
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
        /// Create a serializer that uses the JSON.net serializer
        /// </summary>
        /// <remarks>
        /// This is internal to reduce exposure of JSON.net types so
        /// it is easier to convert to System.Text.Json
        /// </remarks>
        internal CosmosTextJsonSerializer(CosmosSerializationOptions cosmosSerializerOptions)
        {
            this.jsonSerializerOptions = new JsonSerializerOptions()
            {
                IgnoreNullValues = cosmosSerializerOptions.IgnoreNullValues,
                WriteIndented = cosmosSerializerOptions.Indented,
                PropertyNamingPolicy = cosmosSerializerOptions.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase ?
                        JsonNamingPolicy.CamelCase : null
            };
            this.InitializeConverters();
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
                if (stream.Length == 0)
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
            if (stream is MemoryStream)
            {
                return JsonSerializer.Deserialize<T>(memoryStream.ToArray(), jsonSerializerOptions);
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
        private void InitializeConverters()
        {
            this.jsonSerializerOptions.Converters.Add(new TextJsonCosmosSqlQuerySpecConverter());
            this.jsonSerializerOptions.Converters.Add(new TextJsonOfferV2Converter());
        }
    }
}
