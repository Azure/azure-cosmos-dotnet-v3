//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using System.Text;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    /// <summary>
    /// Used to represent an order by item for a cross partition ORDER BY query.
    /// </summary>
    /// <example>{"item": 5}</example>
    [JsonConverter(typeof(OrderByItemConverter))]
    internal struct OrderByItem
    {
        private const string ItemName = "item";
        private static readonly JsonSerializerSettings NoDateParseHandlingJsonSerializerSettings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None
        };

        private readonly CosmosObject cosmosObject;

        public OrderByItem(CosmosElement cosmosElement)
        {
            if (cosmosElement == null)
            {
                throw new ArgumentNullException($"{nameof(cosmosElement)} must not be null.");
            }

            if (!(cosmosElement is CosmosObject cosmosObject))
            {
                throw new ArgumentException($"{nameof(cosmosElement)} must be an object.");
            }

            this.cosmosObject = cosmosObject;
        }

        public CosmosElement Item
        {
            get
            {
                if (!this.cosmosObject.TryGetValue(ItemName, out CosmosElement cosmosElement))
                {
                    throw new InvalidOperationException($"Underlying object does not have an 'item' field.");
                }

                return cosmosElement;
            }
        }

        /// <summary>
        /// Custom converter to serialize and deserialize the payload.
        /// </summary>
        private sealed class OrderByItemConverter : JsonConverter
        {
            /// <summary>
            /// Gets whether or not the object can be converted.
            /// </summary>
            /// <param name="objectType">The type of the object.</param>
            /// <returns>Whether or not the object can be converted.</returns>
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(object);
            }

            /// <summary>
            /// Reads a payload from a json reader.
            /// </summary>
            /// <param name="reader">The reader.</param>
            /// <param name="objectType">The object type.</param>
            /// <param name="existingValue">The existing value.</param>
            /// <param name="serializer">The serialized</param>
            /// <returns>The deserialized JSON.</returns>
            public override object ReadJson(
                JsonReader reader,
                Type objectType,
                object existingValue,
                JsonSerializer serializer)
            {
                JToken jToken = JToken.Load(reader);
                // TODO: In the future we can go from jToken to CosmosElement if we have the eager implemenation.
                CosmosElement cosmosElement = CosmosElement.Create(Encoding.UTF8.GetBytes(jToken.ToString()));
                return new OrderByItem(cosmosElement);
            }

            /// <summary>
            /// Writes the json to a writer.
            /// </summary>
            /// <param name="writer">The writer to write to.</param>
            /// <param name="value">The value to serialize.</param>
            /// <param name="serializer">The serializer to use.</param>
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                writer.WriteStartObject();
                OrderByItem orderByItem = (OrderByItem)value;
                if (orderByItem.Item != null)
                {
                    writer.WritePropertyName(ItemName);
                    new CosmosElementJsonConverter().WriteJson(writer, orderByItem.Item, serializer);
                }

                writer.WriteEndObject();
            }
        }
    }
}
