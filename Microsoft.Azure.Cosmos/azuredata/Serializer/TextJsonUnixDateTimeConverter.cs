// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonUnixDateTimeConverter : JsonConverter<DateTime?>
    {
        private static DateTime UnixStartTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);

        public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.Number)
            {
                throw new JsonException(RMResources.DateTimeConverterInvalidReaderValue);
            }

            return TextJsonUnixDateTimeConverter.ReadProperty(reader.GetString());
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTime? value,
            JsonSerializerOptions options)
        {
            TextJsonUnixDateTimeConverter.WritePropertyValues(writer, value, options);
        }

        public static DateTime? ReadProperty(string value)
        {
            double totalSeconds = 0;
            try
            {
                totalSeconds = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                throw new JsonException(RMResources.DateTimeConveterInvalidReaderDoubleValue);
            }

            return TextJsonUnixDateTimeConverter.UnixStartTime.AddSeconds(totalSeconds);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            DateTime? value,
            JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                Int64 totalSeconds = (Int64)((DateTime)value - TextJsonUnixDateTimeConverter.UnixStartTime).TotalSeconds;
                writer.WriteNumberValue(totalSeconds);
            }
        }
    }
}
