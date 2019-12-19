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

            writer.WriteString(Constants.Properties.Id, setting.Id);

            if (setting.ETag.HasValue)
            {
                writer.WriteString(Constants.Properties.ETag, setting.ETag.ToString());
            }

            if (!string.IsNullOrEmpty(setting.ResourceId))
            {
                writer.WriteString(Constants.Properties.RId, setting.ResourceId);
            }

            writer.WritePropertyName(Constants.Properties.WritableLocations);
            writer.WriteStartArray();
            foreach (AccountRegion accountRegion in setting.WriteLocationsInternal)
            {
                TextJsonAccountRegionConverter.WritePropertyValues(writer, accountRegion);
            }

            writer.WriteEndArray();

            writer.WritePropertyName(Constants.Properties.ReadableLocations);
            writer.WriteStartArray();
            foreach (AccountRegion accountRegion in setting.ReadLocationsInternal)
            {
                TextJsonAccountRegionConverter.WritePropertyValues(writer, accountRegion);
            }

            writer.WriteEndArray();

            writer.WritePropertyName(Constants.Properties.UserConsistencyPolicy);
            TextJsonAccountConsistencyConverter.WritePropertyValue(writer, setting.Consistency, options);

            writer.WriteString(Constants.Properties.AddressesLink, setting.AddressesLink);

            writer.WritePropertyName(Constants.Properties.UserReplicationPolicy);
            TextJsonReplicationPolicyConverter.WritePropertyValue(writer, setting.ReplicationPolicy);

            writer.WritePropertyName(Constants.Properties.SystemReplicationPolicy);
            TextJsonReplicationPolicyConverter.WritePropertyValue(writer, setting.SystemReplicationPolicy);

            writer.WritePropertyName(Constants.Properties.ReadPolicy);
            TextJsonReadPolicyConverter.WritePropertyValue(writer, setting.ReadPolicy);

            writer.WriteString(Constants.Properties.QueryEngineConfiguration, setting.QueryEngineConfigurationString);

            writer.WriteBoolean(Constants.Properties.EnableMultipleWriteLocations, setting.EnableMultipleWriteLocations);

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(
            AccountProperties setting,
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
            else if (property.NameEquals(Constants.Properties.WritableLocations))
            {
                setting.WriteLocationsInternal = new Collection<AccountRegion>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    setting.WriteLocationsInternal.Add(TextJsonAccountRegionConverter.ReadProperty(item));
                }
            }
            else if (property.NameEquals(Constants.Properties.ReadableLocations))
            {
                setting.ReadLocationsInternal = new Collection<AccountRegion>();
                foreach (JsonElement item in property.Value.EnumerateArray())
                {
                    setting.ReadLocationsInternal.Add(TextJsonAccountRegionConverter.ReadProperty(item));
                }
            }
            else if (property.NameEquals(Constants.Properties.UserConsistencyPolicy))
            {
                setting.Consistency = TextJsonAccountConsistencyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(Constants.Properties.AddressesLink))
            {
                setting.AddressesLink = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.UserReplicationPolicy))
            {
                setting.ReplicationPolicy = TextJsonReplicationPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(Constants.Properties.SystemReplicationPolicy))
            {
                setting.SystemReplicationPolicy = TextJsonReplicationPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(Constants.Properties.ReadPolicy))
            {
                setting.ReadPolicy = TextJsonReadPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(Constants.Properties.QueryEngineConfiguration))
            {
                setting.QueryEngineConfigurationString = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.EnableMultipleWriteLocations))
            {
                setting.EnableMultipleWriteLocations = property.Value.GetBoolean();
            }
        }
    }
}
