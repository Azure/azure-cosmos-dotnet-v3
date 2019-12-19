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
            writer.WriteString(Constants.Properties.Id, setting.Id);

            if (setting.ETag.HasValue)
            {
                writer.WriteString(Constants.Properties.ETag, setting.ETag.ToString());
            }

            writer.WriteString(Constants.Properties.ResourceLink, setting.ResourceUri);

            if (!string.IsNullOrEmpty(setting.ResourceId))
            {
                writer.WriteString(Constants.Properties.RId, setting.ResourceId);
            }

            writer.WriteString(Constants.Properties.PermissionMode, JsonSerializer.Serialize(setting.PermissionMode, options));

            if (!string.IsNullOrEmpty(setting.Token))
            {
                writer.WriteString(Constants.Properties.Token, setting.Token);
            }

            if (setting.LastModified.HasValue)
            {
                writer.WritePropertyName(Constants.Properties.LastModified);
                TextJsonUnixDateTimeConverter.WritePropertyValues(writer, setting.LastModified, options);
            }

            if (setting.InternalResourcePartitionKey != null)
            {
                writer.WritePropertyName(Constants.Properties.ResourcePartitionKey);
                TextJsonPartitionKeyInternalConverter.WriteElement(writer, setting.InternalResourcePartitionKey);
            }

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            PermissionProperties setting,
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
                setting.ResourceUri = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.PermissionMode))
            {
                if (Enum.TryParse(property.Value.GetString(), out PermissionMode permissionMode))
                {
                    setting.PermissionMode = permissionMode;
                }
            }
            else if (property.NameEquals(Constants.Properties.Token))
            {
                setting.Token = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.LastModified))
            {
                setting.LastModified = TextJsonUnixDateTimeConverter.ReadProperty(property.Value.GetString());
            }
            else if (property.NameEquals(Constants.Properties.ResourcePartitionKey))
            {
                setting.InternalResourcePartitionKey = TextJsonPartitionKeyInternalConverter.ReadElement(property.Value);
            }
        }
    }
}
