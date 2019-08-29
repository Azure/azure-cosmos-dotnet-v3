//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Routing
{
    using System;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class RangeJsonConverter : JsonConverter
    {
        private static readonly string MinProperty = "min";
        private static readonly string MaxProperty = "max";

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            try
            {
                Range<string> range = (Range<string>)value;

                writer.WriteStartObject();
                writer.WritePropertyName(MinProperty);
                serializer.Serialize(writer, range.Min);
                writer.WritePropertyName(MaxProperty);
                serializer.Serialize(writer, range.Max);
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
                return new Range<string>(
                    jsonObject[MinProperty].Value<string>(),
                    jsonObject[MaxProperty].Value<string>(),
                    true, false);
            }
            catch (Exception ex)
            {
                throw new JsonSerializationException(string.Empty, ex);
            }
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(PartitionKeyRange).IsAssignableFrom(objectType);
        }
    }
}