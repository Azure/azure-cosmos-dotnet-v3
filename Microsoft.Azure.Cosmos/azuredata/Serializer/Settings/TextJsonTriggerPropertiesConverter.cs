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

            writer.WriteString(JsonEncodedStrings.Body, setting.Body);

            writer.WriteString(JsonEncodedStrings.TriggerType, setting.TriggerType.ToString());

            writer.WriteString(JsonEncodedStrings.TriggerOperation, setting.TriggerOperation.ToString());

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            TriggerProperties setting,
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
            else if (property.NameEquals(JsonEncodedStrings.Body.EncodedUtf8Bytes))
            {
                setting.Body = property.Value.GetString();
            }
            else if (property.NameEquals(JsonEncodedStrings.TriggerType.EncodedUtf8Bytes))
            {
                if (Enum.TryParse(value: property.Value.GetString(), ignoreCase: true, out Scripts.TriggerType triggerType))
                {
                    setting.TriggerType = triggerType;
                }
            }
            else if (property.NameEquals(JsonEncodedStrings.TriggerOperation.EncodedUtf8Bytes))
            {
                if (Enum.TryParse(value: property.Value.GetString(), ignoreCase: true, out Scripts.TriggerOperation triggerOperation))
                {
                    setting.TriggerOperation = triggerOperation;
                }
            }
        }
    }
}
