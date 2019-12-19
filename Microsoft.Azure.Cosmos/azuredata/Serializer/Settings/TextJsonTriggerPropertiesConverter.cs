//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Azure.Cosmos.Scripts;
    using Microsoft.Azure.Documents;

    internal class TextJsonTriggerPropertiesConverter : JsonConverter<TriggerProperties>
    {
        public override TriggerProperties Read(
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
            TriggerProperties setting = new TriggerProperties();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonTriggerPropertiesConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        public override void Write(
            Utf8JsonWriter writer,
            TriggerProperties setting,
            JsonSerializerOptions options)
        {
            if (setting == null)
            {
                return;
            }

            writer.WriteStartObject();
            TextJsonSettingsHelper.WriteId(writer, setting.Id);

            TextJsonSettingsHelper.WriteETag(writer, setting.ETag);

            writer.WriteString(Constants.Properties.Body, setting.Body);

            writer.WriteString(Constants.Properties.TriggerType, JsonSerializer.Serialize(setting.TriggerType, options));

            writer.WriteString(Constants.Properties.TriggerOperation, JsonSerializer.Serialize(setting.TriggerOperation, options));

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            TriggerProperties setting,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Id))
            {
                setting.Id = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.ETag))
            {
                setting.ETag = TextJsonSettingsHelper.ReadETag(property);
            }
            else if (property.NameEquals(Constants.Properties.Body))
            {
                setting.Body = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.TriggerType))
            {
                if (Enum.TryParse(property.Value.GetString(), out Scripts.TriggerType triggerType))
                {
                    setting.TriggerType = triggerType;
                }
            }
            else if (property.NameEquals(Constants.Properties.TriggerOperation))
            {
                if (Enum.TryParse(property.Value.GetString(), out Scripts.TriggerOperation triggerOperation))
                {
                    setting.TriggerOperation = triggerOperation;
                }
            }
        }
    }
}
