// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class QueryFeedTokenInternalConverter : JsonConverter
    {
        private const string QueryDefinitionPropertyName = "Query";
        private const string FeedTokenPropertyName = "Token";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(QueryFeedTokenInternal);
        }

        public override object ReadJson(
           JsonReader reader,
           Type objectType,
           object existingValue,
           JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
            }

            JObject jObject = JObject.Load(reader);

            if (!jObject.TryGetValue(QueryFeedTokenInternalConverter.FeedTokenPropertyName, out JToken feedToken))
            {
                throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(QueryFeedTokenInternalConverter.QueryDefinitionPropertyName, out JToken queryToken))
            {
                throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
            }

            QueryDefinition queryDefinition = null;
            if (queryToken.Type != JTokenType.Null)
            {
                queryDefinition = new QueryDefinition(serializer.Deserialize<SqlQuerySpec>(queryToken.CreateReader()));
            }

            IQueryFeedToken queryFeedToken = (IQueryFeedToken)FeedTokenInternalConverter.ReadJObject(feedToken.Value<JObject>(), serializer);

            return new QueryFeedTokenInternal(queryFeedToken, queryDefinition);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            QueryFeedTokenInternal queryFeedTokenInternal = value as QueryFeedTokenInternal;
            writer.WriteStartObject();
            writer.WritePropertyName(QueryFeedTokenInternalConverter.FeedTokenPropertyName);
            serializer.Serialize(writer, queryFeedTokenInternal.QueryFeedToken);
            writer.WritePropertyName(QueryFeedTokenInternalConverter.QueryDefinitionPropertyName);
            if (queryFeedTokenInternal.QueryDefinition != null)
            {
                serializer.Serialize(writer, queryFeedTokenInternal.QueryDefinition.ToSqlQuerySpec());
            }
            else
            {
                writer.WriteNull();
            }

            writer.WriteEndObject();
        }
    }
}
