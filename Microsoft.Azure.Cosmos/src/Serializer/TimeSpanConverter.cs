// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos
{
    using System;
    using Newtonsoft.Json;

    internal class TimeSpanConverter : JsonConverter
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
            if (existingValue is int timeSpanInMinutes)
            {
                return TimeSpan.FromMinutes(timeSpanInMinutes);
            }

            throw new JsonReaderException($"Cannot parse {existingValue} as TimeSpan.");
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(TimeSpan) == objectType;
        }
    }
}
