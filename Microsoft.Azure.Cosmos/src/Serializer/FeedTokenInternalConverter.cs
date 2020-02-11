// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.Azure.Cosmos.Query.Core.ContinuationTokens;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class FeedTokenInternalConverter : JsonConverter
    {
        private static string TypePropertyName = "T";
        private static string VersionPropertyName = "V";
        private static string RidPropertyName = "Rid";
        private static string ContinuationPropertyName = "Continuation";

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
                throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
            }

            JObject jObject = JObject.Load(reader);

            if (!jObject.TryGetValue(FeedTokenInternalConverter.TypePropertyName, out JToken typeJtoken)
                || !Enum.TryParse(typeJtoken.Value<int>().ToString(), ignoreCase: true, out FeedTokenType feedTokenType))
            {
                throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(FeedTokenInternalConverter.RidPropertyName, out JToken ridJToken)
                || string.IsNullOrEmpty(ridJToken.Value<string>()))
            {
                throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
            }

            switch (feedTokenType)
            {
                case FeedTokenType.EPKRange:
                    if (!jObject.TryGetValue(FeedTokenInternalConverter.ContinuationPropertyName, out JToken continuationJToken))
                    {
                        throw new JsonSerializationException(ClientResources.FeedToken_UnknownFormat);
                    }

                    List<CompositeContinuationToken> ranges = serializer.Deserialize<List<CompositeContinuationToken>>(continuationJToken.CreateReader());
                    return new FeedTokenEPKRange(ridJToken.Value<string>(), ranges);
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
                writer.WriteEndObject();
            }
        }
    }
}
