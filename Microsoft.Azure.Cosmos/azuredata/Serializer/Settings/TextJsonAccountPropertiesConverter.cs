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

    internal class TextJsonAccountPropertiesConverter : JsonConverter<AccountProperties>
    {
        public override AccountProperties Read(
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
            AccountProperties setting = new AccountProperties();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonAccountPropertiesConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        public override void Write(
            Utf8JsonWriter writer,
            AccountProperties setting,
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

            writer.WritePropertyName(JsonEncodedStrings.WritableLocations);
            writer.WriteStartArray();
            foreach (AccountRegion accountRegion in setting.WriteLocationsInternal)
            {
                TextJsonAccountRegionConverter.WritePropertyValues(writer, accountRegion);
            }

            writer.WriteEndArray();

            writer.WritePropertyName(JsonEncodedStrings.ReadableLocations);
            writer.WriteStartArray();
            foreach (AccountRegion accountRegion in setting.ReadLocationsInternal)
            {
                TextJsonAccountRegionConverter.WritePropertyValues(writer, accountRegion);
            }

            writer.WriteEndArray();

            writer.WritePropertyName(JsonEncodedStrings.UserConsistencyPolicy);
            TextJsonAccountConsistencyConverter.WritePropertyValue(writer, setting.Consistency, options);

            writer.WriteString(JsonEncodedStrings.AddressesLink, setting.AddressesLink);

            writer.WritePropertyName(JsonEncodedStrings.UserReplicationPolicy);
            TextJsonReplicationPolicyConverter.WritePropertyValue(writer, setting.ReplicationPolicy);

            writer.WritePropertyName(JsonEncodedStrings.SystemReplicationPolicy);
            TextJsonReplicationPolicyConverter.WritePropertyValue(writer, setting.SystemReplicationPolicy);

            writer.WritePropertyName(JsonEncodedStrings.ReadPolicy);
            TextJsonReadPolicyConverter.WritePropertyValue(writer, setting.ReadPolicy);

            writer.WriteString(JsonEncodedStrings.QueryEngineConfiguration, setting.QueryEngineConfigurationString);

            writer.WriteBoolean(JsonEncodedStrings.EnableMultipleWriteLocations, setting.EnableMultipleWriteLocations);

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            AccountProperties setting,
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
            else if (property.NameEquals(JsonEncodedStrings.WritableLocations.EncodedUtf8Bytes))
            {
                setting.WriteLocationsInternal = new Collection<AccountRegion>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    setting.WriteLocationsInternal.Add(TextJsonAccountRegionConverter.ReadProperty(item));
                }
            }
            else if (property.NameEquals(JsonEncodedStrings.ReadableLocations.EncodedUtf8Bytes))
            {
                setting.ReadLocationsInternal = new Collection<AccountRegion>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    setting.ReadLocationsInternal.Add(TextJsonAccountRegionConverter.ReadProperty(item));
                }
            }
            else if (property.NameEquals(JsonEncodedStrings.UserConsistencyPolicy.EncodedUtf8Bytes))
            {
                setting.Consistency = TextJsonAccountConsistencyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(JsonEncodedStrings.AddressesLink.EncodedUtf8Bytes))
            {
                setting.AddressesLink = property.Value.GetString();
            }
            else if (property.NameEquals(JsonEncodedStrings.UserReplicationPolicy.EncodedUtf8Bytes))
            {
                setting.ReplicationPolicy = TextJsonReplicationPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(JsonEncodedStrings.SystemReplicationPolicy.EncodedUtf8Bytes))
            {
                setting.SystemReplicationPolicy = TextJsonReplicationPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(JsonEncodedStrings.ReadPolicy.EncodedUtf8Bytes))
            {
                setting.ReadPolicy = TextJsonReadPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(JsonEncodedStrings.QueryEngineConfiguration.EncodedUtf8Bytes))
            {
                setting.QueryEngineConfigurationString = property.Value.GetString();
            }
            else if (property.NameEquals(JsonEncodedStrings.EnableMultipleWriteLocations.EncodedUtf8Bytes))
            {
                setting.EnableMultipleWriteLocations = property.Value.GetBoolean();
            }
        }
    }
}
