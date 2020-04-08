// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class FeedRangePartitionKeyConverter : JsonConverter
    {
        private const string PartitionKeyPropertyName = "PK";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedRangePartitionKey);
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
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            JObject jObject = JObject.Load(reader);

            return FeedRangePartitionKeyConverter.ReadJObject(jObject, serializer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is FeedRangePartitionKey feedRangePartitionKey)
            {
                writer.WritePropertyName(FeedRangePartitionKeyConverter.PartitionKeyPropertyName);
                writer.WriteValue(feedRangePartitionKey.PartitionKey.ToJsonString());
                return;
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnrecognizedFeedToken);
        }

        public static FeedRangePartitionKey ReadJObject(
            JObject jObject,
            JsonSerializer serializer)
        {
            if (!jObject.TryGetValue(FeedRangePartitionKeyConverter.PartitionKeyPropertyName, out JToken pkJToken))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!PartitionKey.TryParseJsonString(pkJToken.Value<string>(), out PartitionKey partitionKey))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            return new FeedRangePartitionKey(partitionKey);
        }
    }
}