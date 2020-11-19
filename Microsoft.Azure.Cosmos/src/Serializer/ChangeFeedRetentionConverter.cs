// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;

    internal class ChangeFeedRetentionConverter : JsonConverter
    {
        public override void WriteJson(
            JsonWriter writer, 
            object value, 
            JsonSerializer serializer)
        {
            if (value == null)
            {
                return;
            }

            if (value is TimeSpan timeSpan)
            {
                writer.WriteValue((int)timeSpan.TotalMinutes);
                return;
            }

            throw new JsonException("Unsupported TimeSpan format.");
        }

        public override object ReadJson(
            JsonReader reader, 
            Type objectType, 
            object existingValue, 
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Integer)
            {
                int timeSpanInMinutes = Convert.ToInt32(reader.Value);
                return TimeSpan.FromMinutes(timeSpanInMinutes);
            }

            throw new JsonReaderException($"Cannot parse {reader.Value} as TimeSpan.");
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(TimeSpan) == objectType;
        }
    }
}
