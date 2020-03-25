// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class FeedTokenPartitionKeyConverter : JsonConverter
    {
        private const string TypePropertyName = "T";
        private const string VersionPropertyName = "V";
        private const string ContinuationPropertyName = "Continuation";
        private const string PartitionKeyPropertyName = "PK";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedTokenPartitionKey);
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

            if (!jObject.TryGetValue(FeedTokenPartitionKeyConverter.TypePropertyName, out JToken typeJtoken)
                || !Enum.TryParse(typeJtoken.Value<int>().ToString(), ignoreCase: true, out FeedTokenType feedTokenType))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(FeedTokenPartitionKeyConverter.ContinuationPropertyName, out JToken continuationJToken))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(FeedTokenPartitionKeyConverter.PartitionKeyPropertyName, out JToken pkJToken))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!PartitionKey.TryParseJsonString(pkJToken.Value<string>(), out PartitionKey partitionKey))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            FeedTokenPartitionKey feedTokenPartitionKey = new FeedTokenPartitionKey(partitionKey);
            feedTokenPartitionKey.UpdateContinuation(continuationJToken.Value<string>());
            return feedTokenPartitionKey;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is FeedTokenPartitionKey feedTokenPartitionKey)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(FeedTokenPartitionKeyConverter.TypePropertyName);
                writer.WriteValue(FeedTokenType.PartitionKeyValue);
                writer.WritePropertyName(FeedTokenPartitionKeyConverter.VersionPropertyName);
                writer.WriteValue(FeedTokenVersion.V1);
                writer.WritePropertyName(FeedTokenPartitionKeyConverter.PartitionKeyPropertyName);
                writer.WriteValue(feedTokenPartitionKey.PartitionKey.ToJsonString());
                writer.WritePropertyName(FeedTokenPartitionKeyConverter.ContinuationPropertyName);
                serializer.Serialize(writer, feedTokenPartitionKey.GetContinuation());
                writer.WriteEndObject();
                return;
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnrecognizedFeedToken);
        }
    }
}
