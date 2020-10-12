// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Microsoft.Azure.Cosmos.Routing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class FeedRangeInternalConverter : JsonConverter
    {
        private const string RangePropertyName = "Range";
        private const string PartitionKeyPropertyName = "PK";
        private const string PartitionKeyRangeIdPropertyName = "PKRangeId";
        private static readonly RangeJsonConverter rangeJsonConverter = new RangeJsonConverter();

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedRangeEpk)
                || objectType == typeof(FeedRangePartitionKey)
                || objectType == typeof(FeedRangePartitionKeyRange);
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

            return FeedRangeInternalConverter.ReadJObject(jObject, serializer);
        }

        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            writer.WriteStartObject();
            FeedRangeInternalConverter.WriteJObject(writer, value, serializer);
            writer.WriteEndObject();
        }

        public static FeedRangeInternal ReadJObject(
            JObject jObject,
            JsonSerializer serializer)
        {
            if (jObject.TryGetValue(FeedRangeInternalConverter.RangePropertyName, out JToken rangeJToken))
            {
                try
                {
                    Documents.Routing.Range<string> completeRange = (Documents.Routing.Range<string>)rangeJsonConverter.ReadJson(rangeJToken.CreateReader(), typeof(Documents.Routing.Range<string>), null, serializer);
                    return new FeedRangeEpk(completeRange);
                }
                catch (JsonSerializationException)
                {
                    throw new JsonReaderException();
                }
            }

            if (jObject.TryGetValue(FeedRangeInternalConverter.PartitionKeyPropertyName, out JToken pkJToken))
            {
                if (!PartitionKey.TryParseJsonString(pkJToken.Value<string>(), out PartitionKey partitionKey))
                {
                    throw new JsonReaderException();
                }

                return new FeedRangePartitionKey(partitionKey);
            }

            if (jObject.TryGetValue(FeedRangeInternalConverter.PartitionKeyRangeIdPropertyName, out JToken pkRangeJToken))
            {
                return new FeedRangePartitionKeyRange(pkRangeJToken.Value<string>());
            }

            throw new JsonReaderException();
        }

        public static void WriteJObject(
            JsonWriter writer,
            object value,
            JsonSerializer serializer)
        {
            if (value is FeedRangeEpk feedRangeEpk)
            {
                writer.WritePropertyName(FeedRangeInternalConverter.RangePropertyName);
                rangeJsonConverter.WriteJson(writer, feedRangeEpk.Range, serializer);
                return;
            }

            if (value is FeedRangePartitionKey feedRangePartitionKey)
            {
                writer.WritePropertyName(FeedRangeInternalConverter.PartitionKeyPropertyName);
                writer.WriteValue(feedRangePartitionKey.PartitionKey.ToJsonString());
                return;
            }

            if (value is FeedRangePartitionKeyRange feedRangePartitionKeyRange)
            {
                writer.WritePropertyName(FeedRangeInternalConverter.PartitionKeyRangeIdPropertyName);
                writer.WriteValue(feedRangePartitionKeyRange.PartitionKeyRangeId);
                return;
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnrecognizedFeedToken);
        }
    }
}