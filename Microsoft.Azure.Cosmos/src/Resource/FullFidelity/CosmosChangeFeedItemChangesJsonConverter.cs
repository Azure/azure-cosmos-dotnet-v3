//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using Newtonsoft.Json;

    /// <summary>
    /// Used to convert <see cref="ChangeFeedItemChanges{T}"/> that also contains <see cref="ChangeFeedMetadata"/> to and from JSON.
    /// </summary>
    internal sealed class CosmosChangeFeedItemChangesJsonConverter : JsonConverter
    {
        private readonly CosmosSerializer UserSerializer;

        internal CosmosChangeFeedItemChangesJsonConverter(CosmosSerializer userSerializer)
        {
            this.UserSerializer = userSerializer ?? throw new ArgumentNullException(nameof(userSerializer));
        }

        private CosmosChangeFeedItemChangesJsonConverter()
        {
        }

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns>true/false</returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(ChangeFeedItemChanges<>);
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="serializer"></param>
        /// <returns><see cref="ChangeFeedItemChanges{T}"/></returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
#if DEBUG
            StackTrace stackTrace = new StackTrace(new StackFrame(true));
            Console.WriteLine($"type => {objectType.GetType()} => {Environment.NewLine}{stackTrace}");
#endif
            if (reader.TokenType == JsonToken.StartObject)
            {
                JsonSerializer jsonSerializer = JsonSerializer.Create(
                    new JsonSerializerSettings
                    {
                        ContractResolver = new CosmosItemChangesContractResolver()
                    });

                return jsonSerializer.Deserialize(reader, objectType);
            }

            return null;
        }

        /// <summary>
        /// Writes the JSON representation of the object.
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="value"></param>
        /// <param name="serializer"></param>
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        internal static CosmosSerializer CreateChangeFeedItemChangesJsonSerializer(
            CosmosSerializer cosmosSerializer,
            CosmosSerializer propertiesSerializer)
        {
            if (propertiesSerializer is CosmosJsonSerializerWrapper cosmosJsonSerializerWrapper)
            {
                propertiesSerializer = cosmosJsonSerializerWrapper.InternalJsonSerializer;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                Converters = new List<JsonConverter>() { new CosmosChangeFeedItemChangesJsonConverter(cosmosSerializer ?? propertiesSerializer) }
            };

            return new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer(settings));
        }
    }
}
