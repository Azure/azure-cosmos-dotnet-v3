//-----------------------------------------------------------------------
// <copyright file="OrderByQueryResult.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
    using Microsoft.Azure.Cosmos.Internal;
    using Microsoft.Azure.Documents;

    /// <summary>
    /// <para>
    /// For cross partition order by queries a query like "SELECT c.id, c.field_0 ORDER BY r.field_7 gets rewritten as:
    /// SELECT r._rid, [{"item": r.field_7}] AS orderByItems, {"id": r.id, "field_0": r.field_0} AS payload
    /// FROM r
    /// WHERE({ document db - formattable order by query - filter})
    /// ORDER BY r.field_7
    /// </para>
    /// <para>
    /// This is so that the client can parse out the _rid, orderByItems from the actual data / payload,
    /// without scanning the entire document.
    /// </para>
    /// <para>
    /// This struct is used to strongly bind the results of that rewritten query.
    /// </para>
    /// </summary>
    internal sealed class OrderByQueryResult
    {
        /// <summary>
        /// Initializes a new instance of the OrderByQueryResult class.
        /// </summary>
        /// <param name="rid">The rid.</param>
        /// <param name="orderByItems">The order by items.</param>
        /// <param name="payload">The payload.</param>
        public OrderByQueryResult(string rid, QueryItem[] orderByItems, object payload)
        {
            if (string.IsNullOrEmpty(rid))
            {
                throw new ArgumentNullException($"{nameof(rid)} can not be null or empty.");
            }

            if (orderByItems == null)
            {
                throw new ArgumentNullException($"{nameof(orderByItems)} can not be null.");
            }

            if (orderByItems.Length == 0)
            {
                throw new ArgumentException($"{nameof(orderByItems)} can not be empty.");
            }

            this.Rid = rid;
            this.OrderByItems = orderByItems;
            this.Payload = payload;
        }

        /// <summary>
        /// Gets the rid of the document.
        /// </summary>
        [JsonProperty("_rid")]
        public string Rid
        {
            get;
        }

        /// <summary>
        /// Gets the order by items from the document.
        /// </summary>
        [JsonProperty("orderByItems")]
        public QueryItem[] OrderByItems
        {
            get;
        }

        /// <summary>
        /// Gets the actual document.
        /// </summary>
        [JsonProperty("payload")]
        [JsonConverter(typeof(PayloadConverter))]
        public object Payload
        {
            get;
        }

        /// <summary>
        /// Custom converter to serialize and deserialize the payload.
        /// </summary>
        private sealed class PayloadConverter : JsonConverter
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
            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                JToken jToken = JToken.Load(reader);
                if (jToken.Type == JTokenType.Object || jToken.Type == JTokenType.Array)
                {
                    return new QueryResult((JContainer)jToken, null, serializer);
                }
                else
                {
                    return jToken;
                }
            }

            /// <summary>
            /// Writes the json to a writer.
            /// </summary>
            /// <param name="writer">The writer to write to.</param>
            /// <param name="value">The value to serialize.</param>
            /// <param name="serializer">The serializer to use.</param>
            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value);
            }
        }
    }
}
