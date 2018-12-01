//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Query
{
    using System;
    using Microsoft.Azure.Cosmos.Internal;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class OrderByQueryResult
    {
        [JsonProperty("_rid")]
        public string Rid
        {
            get;
            set;
        }

        [JsonProperty("orderByItems")]
        public QueryItem[] OrderByItems
        {
            get;
            set;
        }

        [JsonProperty("payload")]
        [JsonConverter(typeof(PayloadConverter))]
        public object Payload
        {
            get;
            set;
        }

        private sealed class PayloadConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType == typeof(object);
            }

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

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value);
            }
        }
    }
}
