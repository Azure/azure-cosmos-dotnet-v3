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

    internal class TextJsonContainerPropertiesConverter : JsonConverter<ContainerProperties>
    {
        public override ContainerProperties Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            ContainerProperties setting = new ContainerProperties();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonContainerPropertiesConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        public override void Write(Utf8JsonWriter writer, ContainerProperties setting, JsonSerializerOptions options)
        {
            if (setting == null)
            {
                return;
            }

            writer.WriteStartObject();
            TextJsonSettingsHelper.WriteId(writer, setting.Id);

            TextJsonSettingsHelper.WriteETag(writer, setting.ETag);

            TextJsonSettingsHelper.WriteResourceId(writer, setting.ResourceId);

            if (setting.indexingPolicyInternal != null)
            {
                writer.WritePropertyName(Constants.Properties.IndexingPolicy);
                TextJsonIndexingPolicyConverter.WritePropertyValue(writer, setting.indexingPolicyInternal, options);
            }

            if (setting.uniqueKeyPolicyInternal != null)
            {
                writer.WritePropertyName(Constants.Properties.UniqueKeyPolicy);
                TextJsonUniqueKeyPolicyConverter.WritePropertyValues(writer, setting.uniqueKeyPolicyInternal, options);
            }

            if (setting.conflictResolutionInternal != null)
            {
                writer.WritePropertyName(Constants.Properties.ConflictResolutionPolicy);
                TextJsonConflictResolutionPolicyConverter.WritePropertyValues(writer, setting.conflictResolutionInternal, options);
            }

            if (setting.LastModified.HasValue)
            {
                writer.WritePropertyName(Constants.Properties.LastModified);
                TextJsonUnixDateTimeConverter.WritePropertyValues(writer, setting.LastModified, options);
            }

            if (setting.DefaultTimeToLive.HasValue)
            {
                writer.WriteNumber(Constants.Properties.DefaultTimeToLive, setting.DefaultTimeToLive.Value);
            }

            if (setting.PartitionKey != null)
            {
                writer.WritePropertyName(Constants.Properties.PartitionKey);
                writer.WriteStartObject();
                if (setting.PartitionKey.Version.HasValue)
                {
                    writer.WriteNumber(Constants.Properties.PartitionKeyDefinitionVersion, (int)setting.PartitionKey.Version);
                }

                writer.WritePropertyName(Constants.Properties.Paths);
                writer.WriteStartArray();
                foreach (string path in setting.PartitionKey.Paths)
                {
                    writer.WriteStringValue(path);
                }

                writer.WriteEndArray();

                writer.WriteString(Constants.Properties.PartitionKind, setting.PartitionKey.Kind.ToString());

                if (setting.PartitionKey.IsSystemKey.HasValue)
                {
                    writer.WriteBoolean(Constants.Properties.SystemKey, setting.PartitionKey.IsSystemKey.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(ContainerProperties setting, JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Id))
            {
                setting.Id = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.ETag))
            {
                setting.ETag = TextJsonSettingsHelper.ReadETag(property);
            }
            else if (property.NameEquals(Constants.Properties.RId))
            {
                setting.ResourceId = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.IndexingPolicy))
            {
                setting.indexingPolicyInternal = TextJsonIndexingPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(Constants.Properties.UniqueKeyPolicy))
            {
                setting.uniqueKeyPolicyInternal = TextJsonUniqueKeyPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(Constants.Properties.ConflictResolutionPolicy))
            {
                setting.conflictResolutionInternal = TextJsonConflictResolutionPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(Constants.Properties.LastModified))
            {
                setting.LastModified = TextJsonUnixDateTimeConverter.ReadProperty(property);
            }
            else if (property.NameEquals(Constants.Properties.DefaultTimeToLive))
            {
                setting.DefaultTimeToLive = property.Value.GetInt32();
            }
            else if (property.NameEquals(Constants.Properties.PartitionKey))
            {
                setting.PartitionKey = new PartitionKeyDefinition();
                foreach (JsonProperty partitionKeyProperties in property.Value.EnumerateObject())
                {
                    if (partitionKeyProperties.NameEquals(Constants.Properties.PartitionKeyDefinitionVersion))
                    {
                        setting.PartitionKey.Version = (Microsoft.Azure.Documents.PartitionKeyDefinitionVersion)partitionKeyProperties.Value.GetInt32();
                    }
                    else if (partitionKeyProperties.NameEquals(Constants.Properties.Paths))
                    {
                        setting.PartitionKey.Paths = new Collection<string>();
                        foreach (JsonElement item in partitionKeyProperties.Value.EnumerateArray())
                        {
                            setting.PartitionKey.Paths.Add(item.GetString());
                        }
                    }
                    else if (partitionKeyProperties.NameEquals(Constants.Properties.PartitionKind))
                    {
                        if (Enum.TryParse(value: partitionKeyProperties.Value.GetString(), ignoreCase: true, out PartitionKind partitionKind))
                        {
                            setting.PartitionKey.Kind = partitionKind;
                        }
                    }
                    else if (partitionKeyProperties.NameEquals(Constants.Properties.SystemKey))
                    {
                        setting.PartitionKey.IsSystemKey = partitionKeyProperties.Value.GetBoolean();
                    }
                }
            }
        }
    }
}
