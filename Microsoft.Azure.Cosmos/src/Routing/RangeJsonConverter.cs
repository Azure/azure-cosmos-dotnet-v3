//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class RangeJsonConverter : JsonConverter
    {
        private static readonly string MinProperty = "min";
        private static readonly string MaxProperty = "max";
        private static readonly string MinInclusiveProperty = "isMinInclusive";
        private static readonly string MaxInclusiveProperty = "isMaxInclusive";

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            try
            {
                Documents.Routing.Range<string> range = (Documents.Routing.Range<string>)value;

                writer.WriteStartObject();
                writer.WritePropertyName(MinProperty);
                serializer.Serialize(writer, range.Min);
                writer.WritePropertyName(MaxProperty);
                serializer.Serialize(writer, range.Max);
                if (!range.IsMinInclusive)
                {
                    writer.WritePropertyName(MinInclusiveProperty);
                    writer.WriteValue(false);
                }
                if (range.IsMaxInclusive)
                {
                    writer.WritePropertyName(MaxInclusiveProperty);
                    writer.WriteValue(true);
                }

                writer.WriteEndObject();
            }
            catch (Exception ex)
            {
                throw new JsonSerializationException(string.Empty, ex);
            }
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            try
            {
                JObject jsonObject = JObject.Load(reader);
                bool isMinInclusive = true;
                if (jsonObject.TryGetValue(MinInclusiveProperty, out JToken minInclusiveToken))
                {
                    isMinInclusive = (bool)minInclusiveToken;
                }

                bool isMaxInclusive = false;
                if (jsonObject.TryGetValue(MaxInclusiveProperty, out JToken maxInclusiveToken))
                {
                    isMaxInclusive = (bool)maxInclusiveToken;
                }

                return new Documents.Routing.Range<string>(
                    jsonObject[MinProperty].Value<string>(),
                    jsonObject[MaxProperty].Value<string>(),
                    isMinInclusive,
                    isMaxInclusive);
            }
            catch (Exception ex)
            {
                throw new JsonSerializationException(string.Empty, ex);
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(Documents.Routing.Range<string>).IsAssignableFrom(objectType);
        }
    }
}