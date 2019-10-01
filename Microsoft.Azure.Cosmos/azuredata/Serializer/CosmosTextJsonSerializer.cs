//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// The default Cosmos JSON.NET serializer.
    /// </summary>
    internal sealed class CosmosTextJsonSerializer : CosmosSerializer
    {
        private static JsonSerializerOptions DefaultSerializationOptions = new JsonSerializerOptions() { WriteIndented = false };
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
        internal CosmosTextJsonSerializer(CosmosSerializationOptions cosmosSerializerOptions)
        {
            this.jsonSerializerSettings = new JsonSerializerOptions()
            {
                IgnoreNullValues = cosmosSerializerOptions.IgnoreNullValues,
                WriteIndented = cosmosSerializerOptions.Indented,
                //ContractResolver = cosmosSerializerOptions.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase
                //    ? new CamelCasePropertyNamesContractResolver()
                //    : null
            };
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
        /// Convert a Stream to the passed in type.
        /// </summary>
        /// <typeparam name="T">The type of object that should be deserialized</typeparam>
        /// <param name="stream">An open stream that is readable that contains JSON</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>The object representing the deserialized stream</returns>
        public override ValueTask<T> FromStreamAsync<T>(
            Stream stream,
            CancellationToken cancellationToken)
        {
            using (stream)
            {
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    return new ValueTask<T>((T)(object)stream);
                }

                return JsonSerializer.DeserializeAsync<T>(stream, this.jsonSerializerSettings, cancellationToken);
            }
        }

        /// <summary>
        /// Converts an object to a open readable stream
        /// </summary>
        /// <typeparam name="T">The type of object being serialized</typeparam>
        /// <param name="input">The object to be serialized</param>
        /// <param name="cancellationToken">(Optional) <see cref="CancellationToken"/> representing request cancellation.</param>
        /// <returns>An open readable stream containing the JSON of the serialized object</returns>
        public override async Task<Stream> ToStreamAsync<T>(
            T input,
            CancellationToken cancellationToken)
        {
            MemoryStream streamPayload = new MemoryStream();
            await JsonSerializer.SerializeAsync<T>(streamPayload, input, CosmosTextJsonSerializer.DefaultSerializationOptions, cancellationToken);
            streamPayload.Position = 0;
            return streamPayload;
        }
    }
}
