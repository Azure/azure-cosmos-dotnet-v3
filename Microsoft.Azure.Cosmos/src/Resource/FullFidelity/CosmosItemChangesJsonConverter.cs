//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Used to convert <see cref="ItemChanges{T}"/> that also contains <see cref="ChangeFeedMetadata"/> to and from JSON.
    /// </summary>
    internal sealed class CosmosItemChangesJsonConverter : JsonConverter
    {
        private readonly CosmosSerializer UserSerializer;

        internal CosmosItemChangesJsonConverter(CosmosSerializer userSerializer)
        {
            this.UserSerializer = userSerializer ?? throw new ArgumentNullException(nameof(userSerializer));
        }

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType"></param>
        /// <returns>true/false</returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(ItemChanges<>);
        }

        /// <summary>
        /// Reads the JSON representation of the object.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="objectType"></param>
        /// <param name="existingValue"></param>
        /// <param name="serializer"></param>
        /// <returns><see cref="ItemChanges{T}"/></returns>
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.StartObject)
            {
                JObject jObject = JObject.Load(reader);

                return JsonConvert.DeserializeObject(
                    value: jObject.ToString(),
                    type: objectType,
                    settings: new JsonSerializerSettings
                    {
                        ContractResolver = new CosmosItemChangesContractResolver()
                    });
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

        internal static CosmosSerializer CreateItemChangesJsonSerializer(
            CosmosSerializer cosmosSerializer, 
            CosmosSerializer propertiesSerializer)
        {
            if (propertiesSerializer is CosmosJsonSerializerWrapper cosmosJsonSerializerWrapper)
            {
                propertiesSerializer = cosmosJsonSerializerWrapper.InternalJsonSerializer;
            }

            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                Converters = new List<JsonConverter>() { new CosmosItemChangesJsonConverter(cosmosSerializer ?? propertiesSerializer) }
            };

            return new CosmosJsonSerializerWrapper(new CosmosJsonDotNetSerializer(settings));
        }
    }
}
