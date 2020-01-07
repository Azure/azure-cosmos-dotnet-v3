//-----------------------------------------------------------------------
// <copyright file="CrossPartitionQueryTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Azure.Cosmos.EmulatorTests
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    // Migrated from https://github.com/JamesNK/Newtonsoft.Json/blob/master/Src/Newtonsoft.Json/Converters/IsoDateTimeConverter.cs
    public class IsoDateTimeConverter : JsonConverter<DateTime?>
    {
        private const string DefaultDateTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";
        private DateTimeStyles dateTimeStyles = DateTimeStyles.RoundtripKind;

        public override DateTime? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException($"Unexpected token parsing date. Expected String, got {reader.TokenType}.");
            }

            string dateText = reader.GetString();
            if (string.IsNullOrEmpty(dateText))
            {
                return null;
            }

            return DateTime.Parse(dateText, CultureInfo.CurrentCulture, this.dateTimeStyles);
        }

        public override void Write(
            Utf8JsonWriter writer,
            DateTime? dateTime,
            JsonSerializerOptions options)
        {
            if (!dateTime.HasValue)
            {
                writer.WriteNullValue();
                return;
            }

            string text = dateTime.Value.ToString(IsoDateTimeConverter.DefaultDateTimeFormat, CultureInfo.CurrentCulture);
            writer.WriteStringValue(text);
        }
    }
}
