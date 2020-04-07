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

    internal sealed class FeedRangeSimpleContinuationConverter : JsonConverter
    {
        private const string TypePropertyName = "T";
        private const string VersionPropertyName = "V";
        private const string RidPropertyName = "Rid";
        private const string ContinuationPropertyName = "Continuation";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedRangeSimpleContinuation);
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

            if (!jObject.TryGetValue(FeedRangeSimpleContinuationConverter.TypePropertyName, out JToken typeJtoken)
                || !Enum.TryParse(typeJtoken.Value<int>().ToString(), ignoreCase: true, out FeedRangeContinuationType tokenType)
                || !FeedRangeContinuationType.Simple.Equals(tokenType))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(FeedRangeSimpleContinuationConverter.RidPropertyName, out JToken ridJToken)
                || string.IsNullOrEmpty(ridJToken.Value<string>()))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(FeedRangeSimpleContinuationConverter.ContinuationPropertyName, out JToken continuationJToken))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!FeedRangeInternal.TryParse(jObject, serializer, out FeedRangeInternal feedRangeInternal))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            return new FeedRangeSimpleContinuation(
                containerRid: ridJToken.Value<string>(),
                feedRange: feedRangeInternal,
                continuation: continuationJToken.Value<string>());
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is FeedRangeSimpleContinuation feedRangeSimpleContinuation)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(FeedRangeSimpleContinuationConverter.TypePropertyName);
                writer.WriteValue(FeedRangeContinuationType.Composite);
                writer.WritePropertyName(FeedRangeSimpleContinuationConverter.VersionPropertyName);
                writer.WriteValue(FeedRangeContinuationVersion.V1);
                writer.WritePropertyName(FeedRangeSimpleContinuationConverter.RidPropertyName);
                writer.WriteValue(feedRangeSimpleContinuation.ContainerRid);
                writer.WritePropertyName(FeedRangeSimpleContinuationConverter.ContinuationPropertyName);
                writer.WriteValue(feedRangeSimpleContinuation.GetContinuation());
                serializer.Serialize(writer, feedRangeSimpleContinuation.FeedRange);
                writer.WriteEndObject();
                return;
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnrecognizedFeedToken);
        }
    }
}
