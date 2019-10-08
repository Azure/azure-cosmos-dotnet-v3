// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Data.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class UnixDateTimeConverter : JsonConverter<DateTime?>
    {
        private static DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.Number)
            {
                throw new JsonException(RMResources.DateTimeConverterInvalidReaderValue);
            }

            double totalSeconds = 0;
            try
            {
                totalSeconds = Convert.ToDouble(reader.GetString(), CultureInfo.InvariantCulture);
            }
            catch
            {
                throw new JsonException(RMResources.DateTimeConveterInvalidReaderDoubleValue);
            }

            return UnixDateTimeConverter.UnixStartTime.AddSeconds(totalSeconds);
        }

        public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
        {
            Int64 totalSeconds = (Int64)((DateTime)value - UnixDateTimeConverter.UnixStartTime).TotalSeconds;
            writer.WriteNumberValue(totalSeconds);
        }
    }
}
