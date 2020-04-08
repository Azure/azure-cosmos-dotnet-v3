// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class FeedRangeEPKConverter : JsonConverter
    {
        private const string RangePropertyName = "Range";

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(FeedRangeEPK);
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

            return FeedRangeEPKConverter.ReadJObject(jObject, serializer);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is FeedRangeEPK feedRangeEPK)
            {
                writer.WritePropertyName(FeedRangeEPKConverter.RangePropertyName);
                serializer.Serialize(writer, feedRangeEPK.Range);
                return;
            }

            throw new JsonSerializationException(ClientResources.FeedToken_UnrecognizedFeedToken);
        }

        public static FeedRangeEPK ReadJObject(
            JObject jObject,
            JsonSerializer serializer)
        {
            if (!jObject.TryGetValue(FeedRangeEPKConverter.RangePropertyName, out JToken rangeJToken))
            {
                throw new JsonReaderException();
            }

            Documents.Routing.Range<string> completeRange = serializer.Deserialize<Documents.Routing.Range<string>>(rangeJToken.CreateReader());
            return new FeedRangeEPK(completeRange);
        }
    }
}