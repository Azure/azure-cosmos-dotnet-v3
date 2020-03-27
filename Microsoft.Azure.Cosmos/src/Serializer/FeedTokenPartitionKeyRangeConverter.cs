// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class FeedTokenPartitionKeyRangeConverter : JsonConverter
    {
        private const string TypePropertyName = "T";
        private const string VersionPropertyName = "V";
        private const string ContinuationPropertyName = "Continuation";
        private const string PartitionKeyRangeIdPropertyName = "PKRangeId";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedTokenPartitionKeyRange);
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

            return FeedTokenPartitionKeyRangeConverter.ReadJObject(jObject);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is FeedTokenPartitionKeyRange feedTokenPartitionKeyRange)
            {
                writer.WriteStartObject();
                writer.WritePropertyName(FeedTokenPartitionKeyRangeConverter.TypePropertyName);
                writer.WriteValue(FeedTokenType.PartitionKeyRangeId);
                writer.WritePropertyName(FeedTokenPartitionKeyRangeConverter.VersionPropertyName);
                writer.WriteValue(FeedTokenVersion.V1);
                writer.WritePropertyName(FeedTokenPartitionKeyRangeConverter.PartitionKeyRangeIdPropertyName);
                serializer.Serialize(writer, feedTokenPartitionKeyRange.PartitionKeyRangeId);
                writer.WritePropertyName(FeedTokenPartitionKeyRangeConverter.ContinuationPropertyName);
                serializer.Serialize(writer, feedTokenPartitionKeyRange.GetContinuation());
                writer.WriteEndObject();
                return;
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnrecognizedFeedToken);
        }

        public static object ReadJObject(JObject jObject)
        {
            if (!jObject.TryGetValue(FeedTokenPartitionKeyRangeConverter.TypePropertyName, out JToken typeJtoken)
                || !Enum.TryParse(typeJtoken.Value<int>().ToString(), ignoreCase: true, out FeedTokenType feedTokenType))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(FeedTokenPartitionKeyRangeConverter.ContinuationPropertyName, out JToken continuationJToken))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            if (!jObject.TryGetValue(FeedTokenPartitionKeyRangeConverter.PartitionKeyRangeIdPropertyName, out JToken pkJToken))
            {
                throw new JsonReaderException(ClientResources.FeedToken_UnknownFormat);
            }

            return new FeedTokenPartitionKeyRange(pkJToken.Value<string>(), continuationJToken.Value<string>());
        }
    }
}
