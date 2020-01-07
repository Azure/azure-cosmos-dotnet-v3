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

            TextJsonSettingsHelper.WriteId(writer, setting.Id);

            TextJsonSettingsHelper.WriteETag(writer, setting.ETag);

            TextJsonSettingsHelper.WriteResourceId(writer, setting.ResourceId);

            TextJsonSettingsHelper.WriteLastModified(writer, setting.LastModified, options);

            if (!string.IsNullOrEmpty(setting.SelfLink))
            {
                writer.WriteString(JsonEncodedStrings.SelfLink, setting.SelfLink);
            }

            if (!string.IsNullOrEmpty(setting.Permissions))
            {
                writer.WriteString(JsonEncodedStrings.PermissionsLink, setting.Permissions);
            }

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            UserProperties setting,
            JsonProperty property)
        {
            if (property.NameEquals(JsonEncodedStrings.Id.EncodedUtf8Bytes))
            {
                setting.Id = property.Value.GetString();
            }
            else if (property.NameEquals(JsonEncodedStrings.ETag.EncodedUtf8Bytes))
            {
                setting.ETag = TextJsonSettingsHelper.ReadETag(property);
            }
            else if (property.NameEquals(JsonEncodedStrings.RId.EncodedUtf8Bytes))
            {
                setting.ResourceId = property.Value.GetString();
            }
            else if (property.NameEquals(JsonEncodedStrings.ResourceLink.EncodedUtf8Bytes))
            {
                setting.ResourceId = property.Value.GetString();
            }
            else if (property.NameEquals(JsonEncodedStrings.SelfLink.EncodedUtf8Bytes))
            {
                setting.SelfLink = property.Value.GetString();
            }
            else if (property.NameEquals(JsonEncodedStrings.LastModified.EncodedUtf8Bytes))
            {
                setting.LastModified = TextJsonUnixDateTimeConverter.ReadProperty(property);
            }
            else if (property.NameEquals(JsonEncodedStrings.PermissionsLink.EncodedUtf8Bytes))
            {
                setting.Permissions = property.Value.GetString();
            }
        }
    }
}
