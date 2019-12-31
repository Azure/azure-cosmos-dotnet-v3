//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonPermissionPropertiesConverter : JsonConverter<PermissionProperties>
    {
        public override PermissionProperties Read(
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
            PermissionProperties setting = new PermissionProperties();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonPermissionPropertiesConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        public override void Write(
            Utf8JsonWriter writer,
            PermissionProperties setting,
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

            writer.WriteString(JsonEncodedStrings.ResourceLink, setting.ResourceUri);

            writer.WriteString(JsonEncodedStrings.PermissionMode, setting.PermissionMode.ToString());

            if (!string.IsNullOrEmpty(setting.Token))
            {
                writer.WriteString(JsonEncodedStrings.Token, setting.Token);
            }

            if (setting.InternalResourcePartitionKey != null)
            {
                writer.WritePropertyName(JsonEncodedStrings.ResourcePartitionKey);
                TextJsonPartitionKeyInternalConverter.WriteElement(writer, setting.InternalResourcePartitionKey);
            }

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            PermissionProperties setting,
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
                setting.ResourceUri = property.Value.GetString();
            }
            else if (property.NameEquals(JsonEncodedStrings.PermissionMode.EncodedUtf8Bytes))
            {
                TextJsonSettingsHelper.TryParseEnum<PermissionMode>(property, permissionMode => setting.PermissionMode = permissionMode);
            }
            else if (property.NameEquals(JsonEncodedStrings.Token.EncodedUtf8Bytes))
            {
                setting.Token = property.Value.GetString();
            }
            else if (property.NameEquals(JsonEncodedStrings.LastModified.EncodedUtf8Bytes))
            {
                setting.LastModified = TextJsonUnixDateTimeConverter.ReadProperty(property);
            }
            else if (property.NameEquals(JsonEncodedStrings.ResourcePartitionKey.EncodedUtf8Bytes))
            {
                setting.InternalResourcePartitionKey = TextJsonPartitionKeyInternalConverter.ReadElement(property.Value);
            }
        }
    }
}
