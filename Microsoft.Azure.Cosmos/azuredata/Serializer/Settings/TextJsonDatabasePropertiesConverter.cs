//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonDatabasePropertiesConverter : JsonConverter<DatabaseProperties>
    {
        public override DatabaseProperties Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.JsonUnexpectedToken));
            }

            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            DatabaseProperties setting = new DatabaseProperties();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonDatabasePropertiesConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        public override void Write(
            Utf8JsonWriter writer,
            DatabaseProperties setting,
            JsonSerializerOptions options)
        {
            if (setting == null)
            {
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(Constants.Properties.Id, setting.Id);

            writer.WriteString(Constants.Properties.ETag, setting.ETag.ToString());

            writer.WriteString(Constants.Properties.RId, setting.ResourceId);

            if (setting.LastModified.HasValue)
            {
                writer.WritePropertyName(Constants.Properties.LastModified);
                TextJsonUnixDateTimeConverter.WritePropertyValues(writer, setting.LastModified, options);
            }

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            DatabaseProperties setting,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Id))
            {
                setting.Id = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.ETag))
            {
                setting.ETag = new ETag(property.Value.GetString());
            }
            else if (property.NameEquals(Constants.Properties.RId))
            {
                setting.ResourceId = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.LastModified))
            {
                setting.LastModified = TextJsonUnixDateTimeConverter.ReadProperty(property.Value.GetString());
            }
        }
    }
}
