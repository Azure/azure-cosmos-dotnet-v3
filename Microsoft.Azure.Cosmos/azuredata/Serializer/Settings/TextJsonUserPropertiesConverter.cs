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

    internal class TextJsonUserPropertiesConverter : JsonConverter<UserProperties>
    {
        public override UserProperties Read(
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
            UserProperties setting = new UserProperties();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonUserPropertiesConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        public override void Write(
            Utf8JsonWriter writer,
            UserProperties setting,
            JsonSerializerOptions options)
        {
            if (setting == null)
            {
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(Constants.Properties.Id, setting.Id);

            if (setting.ETag.HasValue)
            {
                writer.WriteString(Constants.Properties.ETag, setting.ETag.ToString());
            }

            if (!string.IsNullOrEmpty(setting.ResourceId))
            {
                writer.WriteString(Constants.Properties.RId, setting.ResourceId);
            }

            if (!string.IsNullOrEmpty(setting.SelfLink))
            {
                writer.WriteString(Constants.Properties.SelfLink, setting.SelfLink);
            }

            if (!string.IsNullOrEmpty(setting.Permissions))
            {
                writer.WriteString(Constants.Properties.PermissionsLink, setting.Permissions);
            }

            if (setting.LastModified.HasValue)
            {
                writer.WritePropertyName(Constants.Properties.LastModified);
                TextJsonUnixDateTimeConverter.WritePropertyValues(writer, setting.LastModified, options);
            }

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            UserProperties setting,
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
            else if (property.NameEquals(Constants.Properties.ResourceLink))
            {
                setting.ResourceId = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.SelfLink))
            {
                setting.SelfLink = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.LastModified))
            {
                setting.LastModified = TextJsonUnixDateTimeConverter.ReadProperty(property.Value.GetString());
            }
            else if (property.NameEquals(Constants.Properties.PermissionsLink))
            {
                setting.Permissions = property.Value.GetString();
            }
        }
    }
}
