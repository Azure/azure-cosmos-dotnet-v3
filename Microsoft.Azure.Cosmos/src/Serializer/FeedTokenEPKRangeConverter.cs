// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class FeedTokenEPKRangeConverter : JsonConverter
    {
        private const string TypePropertyName = "T";
        private const string VersionPropertyName = "V";
        private const string RangePropertyName = "Range";
        private const string RidPropertyName = "Rid";
        private const string ContinuationPropertyName = "Continuation";
        private const string PartitionKeyRangeIdPropertyName = "PKRangeId";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedTokenEPKRange);
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

            return FeedTokenEPKRangeConverter.ReadJObject(jObject, serializer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is FeedTokenEPKRange feedTokenEPKRange)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(FeedTokenEPKRangeConverter.TypePropertyName);
                writer.WriteValue(FeedTokenType.EPKRange);
                writer.WritePropertyName(FeedTokenEPKRangeConverter.VersionPropertyName);
                writer.WriteValue(FeedTokenVersion.V1);
                writer.WritePropertyName(FeedTokenEPKRangeConverter.RidPropertyName);
                writer.WriteValue(feedTokenEPKRange.ContainerRid);
                writer.WritePropertyName(FeedTokenEPKRangeConverter.ContinuationPropertyName);
                serializer.Serialize(writer, feedTokenEPKRange.CompositeContinuationTokens.ToArray());
                writer.WritePropertyName(FeedTokenEPKRangeConverter.RangePropertyName);
                serializer.Serialize(writer, feedTokenEPKRange.CompleteRange);
                writer.WriteEndObject();
                return;
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnrecognizedFeedToken);
        }

        public static object ReadJObject(
            JObject jObject,
            JsonSerializer serializer)
        {
            if (!jObject.TryGetValue(FeedTokenEPKRangeConverter.RidPropertyName, out JToken ridJToken)
                            || string.IsNullOrEmpty(ridJToken.Value<string>()))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(FeedTokenEPKRangeConverter.ContinuationPropertyName, out JToken continuationJToken))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(FeedTokenEPKRangeConverter.RangePropertyName, out JToken rangeJToken))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            List<CompositeContinuationToken> ranges = serializer.Deserialize<List<CompositeContinuationToken>>(continuationJToken.CreateReader());
            Documents.Routing.Range<string> completeRange = serializer.Deserialize<Documents.Routing.Range<string>>(rangeJToken.CreateReader());
            return new FeedTokenEPKRange(ridJToken.Value<string>(), completeRange, ranges);
        }
    }
}
