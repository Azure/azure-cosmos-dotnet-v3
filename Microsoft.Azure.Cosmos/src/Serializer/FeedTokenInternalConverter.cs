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

    internal sealed class FeedTokenInternalConverter : JsonConverter
    {
        private static string TypePropertyName = "T";
        private static string VersionPropertyName = "V";
        private static string RangePropertyName = "Range";
        private static string RidPropertyName = "Rid";
        private static string ContinuationPropertyName = "Continuation";
        private static string PartitionKeyPropertyName = "PK";
        private static string PartitionKeyRangeIdPropertyName = "PKRangeId";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedTokenEPKRange)
                || objectType == typeof(FeedTokenPartitionKey)
                || objectType == typeof(FeedTokenPartitionKeyRange);
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

            if (!jObject.TryGetValue(FeedTokenInternalConverter.TypePropertyName, out JToken typeJtoken)
                || !Enum.TryParse(typeJtoken.Value<int>().ToString(), ignoreCase: true, out FeedTokenType feedTokenType))
            {
                throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
            }

            switch (feedTokenType)
            {
                case FeedTokenType.EPKRange:
                    {
                        if (!jObject.TryGetValue(FeedTokenInternalConverter.RidPropertyName, out JToken ridJToken)
                            || string.IsNullOrEmpty(ridJToken.Value<string>()))
                        {
                            throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
                        }

                        if (!jObject.TryGetValue(FeedTokenInternalConverter.ContinuationPropertyName, out JToken continuationJToken))
                        {
                            throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
                        }

                        if (!jObject.TryGetValue(FeedTokenInternalConverter.RangePropertyName, out JToken rangeJToken))
                        {
                            throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
                        }

                        List<CompositeContinuationToken> ranges = serializer.Deserialize<List<CompositeContinuationToken>>(continuationJToken.CreateReader());
                        Documents.Routing.Range<string> completeRange = serializer.Deserialize<Documents.Routing.Range<string>>(rangeJToken.CreateReader());
                        return new FeedTokenEPKRange(ridJToken.Value<string>(), completeRange, ranges);
                    }
                case FeedTokenType.PartitionKeyValue:
                    {
                        if (!jObject.TryGetValue(FeedTokenInternalConverter.ContinuationPropertyName, out JToken continuationJToken))
                        {
                            throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
                        }

                        if (!jObject.TryGetValue(FeedTokenInternalConverter.PartitionKeyPropertyName, out JToken pkJToken))
                        {
                            throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
                        }

                        if (!PartitionKey.TryParseJsonString(pkJToken.Value<string>(), out PartitionKey partitionKey))
                        {
                            throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
                        }

                        FeedTokenPartitionKey feedTokenPartitionKey = new FeedTokenPartitionKey(partitionKey);
                        feedTokenPartitionKey.UpdateContinuation(continuationJToken.Value<string>());
                        return feedTokenPartitionKey;
                    }
                case FeedTokenType.PartitionKeyRangeId:
                    {
                        if (!jObject.TryGetValue(FeedTokenInternalConverter.ContinuationPropertyName, out JToken continuationJToken))
                        {
                            throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
                        }

                        if (!jObject.TryGetValue(FeedTokenInternalConverter.PartitionKeyRangeIdPropertyName, out JToken pkJToken))
                        {
                            throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
                        }

                        FeedTokenPartitionKeyRange feedTokenPartitionKeyRange = new FeedTokenPartitionKeyRange(pkJToken.Value<string>());
                        feedTokenPartitionKeyRange.UpdateContinuation(continuationJToken.Value<string>());
                        return feedTokenPartitionKeyRange;
                    }
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is FeedTokenEPKRange feedTokenEPKRange)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(FeedTokenInternalConverter.TypePropertyName);
                writer.WriteValue(FeedTokenType.EPKRange);
                writer.WritePropertyName(FeedTokenInternalConverter.VersionPropertyName);
                writer.WriteValue(FeedTokenVersion.V1);
                writer.WritePropertyName(FeedTokenInternalConverter.RidPropertyName);
                writer.WriteValue(feedTokenEPKRange.ContainerRid);
                writer.WritePropertyName(FeedTokenInternalConverter.ContinuationPropertyName);
                serializer.Serialize(writer, feedTokenEPKRange.CompositeContinuationTokens.ToArray());
                writer.WritePropertyName(FeedTokenInternalConverter.RangePropertyName);
                serializer.Serialize(writer, feedTokenEPKRange.CompleteRange);
                writer.WriteEndObject();
                return;
            }

            if (value is FeedTokenPartitionKey feedTokenPartitionKey)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(FeedTokenInternalConverter.TypePropertyName);
                writer.WriteValue(FeedTokenType.PartitionKeyValue);
                writer.WritePropertyName(FeedTokenInternalConverter.VersionPropertyName);
                writer.WriteValue(FeedTokenVersion.V1);
                writer.WritePropertyName(FeedTokenInternalConverter.PartitionKeyPropertyName);
                writer.WriteValue(feedTokenPartitionKey.PartitionKey.ToJsonString());
                writer.WritePropertyName(FeedTokenInternalConverter.ContinuationPropertyName);
                serializer.Serialize(writer, feedTokenPartitionKey.GetContinuation());
                writer.WriteEndObject();
                return;
            }

            if (value is FeedTokenPartitionKeyRange feedTokenPartitionKeyRange)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(FeedTokenInternalConverter.TypePropertyName);
                writer.WriteValue(FeedTokenType.PartitionKeyRangeId);
                writer.WritePropertyName(FeedTokenInternalConverter.VersionPropertyName);
                writer.WriteValue(FeedTokenVersion.V1);
                writer.WritePropertyName(FeedTokenInternalConverter.PartitionKeyRangeIdPropertyName);
                serializer.Serialize(writer, feedTokenPartitionKeyRange.PartitionKeyRangeId);
                writer.WritePropertyName(FeedTokenInternalConverter.ContinuationPropertyName);
                serializer.Serialize(writer, feedTokenPartitionKeyRange.GetContinuation());
                writer.WriteEndObject();
                return;
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnrecognizedFeedToken);
        }
    }
}
