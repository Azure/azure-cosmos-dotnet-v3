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

    internal class TextJsonUserDefinedFunctionPropertiesConverter : JsonConverter<UserDefinedFunctionProperties>
    {
        public override UserDefinedFunctionProperties Read(
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
            UserDefinedFunctionProperties setting = new UserDefinedFunctionProperties();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonUserDefinedFunctionPropertiesConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        public override void Write(
            Utf8JsonWriter writer,
            UserDefinedFunctionProperties setting,
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

            writer.WriteString(Constants.Properties.Body, setting.Body);

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            UserDefinedFunctionProperties setting,
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
            else if (property.NameEquals(Constants.Properties.Body))
            {
                setting.Body = property.Value.GetString();
            }
        }
    }
}
