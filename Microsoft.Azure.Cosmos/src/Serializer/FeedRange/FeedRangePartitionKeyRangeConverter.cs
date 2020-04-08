// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class FeedRangePartitionKeyRangeConverter : JsonConverter
    {
        private const string PartitionKeyRangeIdPropertyName = "PKRangeId";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedRangePartitionKeyRange);
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
                throw new JsonReaderException();
            }

            JObject jObject = JObject.Load(reader);

            return FeedRangePartitionKeyRangeConverter.ReadJObject(jObject, serializer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is FeedRangePartitionKeyRange feedRangePartitionKeyRange)
            {
                writer.WritePropertyName(FeedRangePartitionKeyRangeConverter.PartitionKeyRangeIdPropertyName);
                writer.WriteValue(feedRangePartitionKeyRange.PartitionKeyRangeId);
                return;
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnrecognizedFeedToken);
        }

        public static FeedRangePartitionKeyRange ReadJObject(
            JObject jObject,
            JsonSerializer serializer)
        {
            if (!jObject.TryGetValue(FeedRangePartitionKeyRangeConverter.PartitionKeyRangeIdPropertyName, out JToken pkRangeJToken))
            {
                throw new JsonReaderException();
            }

            return new FeedRangePartitionKeyRange(pkRangeJToken.Value<string>());
        }
    }
}