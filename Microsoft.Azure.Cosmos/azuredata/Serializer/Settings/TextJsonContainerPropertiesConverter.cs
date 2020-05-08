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

    internal class TextJsonContainerPropertiesConverter : JsonConverter<CosmosContainerProperties>
    {
        public override CosmosContainerProperties Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            CosmosContainerProperties setting = new CosmosContainerProperties();

            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonContainerPropertiesConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        public override void Write(Utf8JsonWriter writer, CosmosContainerProperties setting, JsonSerializerOptions options)
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

            if (setting.indexingPolicyInternal != null)
            {
                writer.WritePropertyName(JsonEncodedStrings.IndexingPolicy);
                TextJsonIndexingPolicyConverter.WritePropertyValue(writer, setting.indexingPolicyInternal, options);
            }

            if (setting.uniqueKeyPolicyInternal != null)
            {
                writer.WritePropertyName(JsonEncodedStrings.UniqueKeyPolicy);
                TextJsonUniqueKeyPolicyConverter.WritePropertyValues(writer, setting.uniqueKeyPolicyInternal, options);
            }

            if (setting.conflictResolutionInternal != null)
            {
                writer.WritePropertyName(JsonEncodedStrings.ConflictResolutionPolicy);
                TextJsonConflictResolutionPolicyConverter.WritePropertyValues(writer, setting.conflictResolutionInternal, options);
            }

            if (setting.DefaultTimeToLive.HasValue)
            {
                writer.WriteNumber(JsonEncodedStrings.DefaultTimeToLive, setting.DefaultTimeToLive.Value);
            }

            if (setting.PartitionKey != null)
            {
                writer.WritePropertyName(JsonEncodedStrings.PartitionKey);
                writer.WriteStartObject();
                if (setting.PartitionKey.Version.HasValue)
                {
                    writer.WriteNumber(JsonEncodedStrings.PartitionKeyDefinitionVersion, (int)setting.PartitionKey.Version);
                }

                writer.WritePropertyName(JsonEncodedStrings.Paths);
                writer.WriteStartArray();
                foreach (string path in setting.PartitionKey.Paths)
                {
                    writer.WriteStringValue(path);
                }

                writer.WriteEndArray();

                writer.WriteString(JsonEncodedStrings.PartitionKind, setting.PartitionKey.Kind.ToString());

                if (setting.PartitionKey.IsSystemKey.HasValue)
                {
                    writer.WriteBoolean(JsonEncodedStrings.SystemKey, setting.PartitionKey.IsSystemKey.Value);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        private static void ReadPropertyValue(CosmosContainerProperties setting, JsonProperty property)
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
            else if (property.NameEquals(JsonEncodedStrings.IndexingPolicy.EncodedUtf8Bytes))
            {
                setting.indexingPolicyInternal = TextJsonIndexingPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(JsonEncodedStrings.UniqueKeyPolicy.EncodedUtf8Bytes))
            {
                setting.uniqueKeyPolicyInternal = TextJsonUniqueKeyPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(JsonEncodedStrings.ConflictResolutionPolicy.EncodedUtf8Bytes))
            {
                setting.conflictResolutionInternal = TextJsonConflictResolutionPolicyConverter.ReadProperty(property.Value);
            }
            else if (property.NameEquals(JsonEncodedStrings.LastModified.EncodedUtf8Bytes))
            {
                setting.LastModified = TextJsonUnixDateTimeConverter.ReadProperty(property);
            }
            else if (property.NameEquals(JsonEncodedStrings.DefaultTimeToLive.EncodedUtf8Bytes))
            {
                setting.DefaultTimeToLive = property.Value.GetInt32();
            }
            else if (property.NameEquals(JsonEncodedStrings.PartitionKey.EncodedUtf8Bytes))
            {
                setting.PartitionKey = new PartitionKeyDefinition();
                foreach (JsonProperty partitionKeyProperties in property.Value.EnumerateObject())
                {
                    if (partitionKeyProperties.NameEquals(JsonEncodedStrings.PartitionKeyDefinitionVersion.EncodedUtf8Bytes))
                    {
                        setting.PartitionKey.Version = (Microsoft.Azure.Documents.PartitionKeyDefinitionVersion)partitionKeyProperties.Value.GetInt32();
                    }
                    else if (partitionKeyProperties.NameEquals(JsonEncodedStrings.Paths.EncodedUtf8Bytes))
                    {
                        setting.PartitionKey.Paths = new Collection<string>();
                        foreach (JsonElement item in partitionKeyProperties.Value.EnumerateArray())
                        {
                            setting.PartitionKey.Paths.Add(item.GetString());
                        }
                    }
                    else if (partitionKeyProperties.NameEquals(JsonEncodedStrings.PartitionKind.EncodedUtf8Bytes))
                    {
                        TextJsonSettingsHelper.TryParseEnum<PartitionKind>(partitionKeyProperties, partitionKind => setting.PartitionKey.Kind = partitionKind);
                    }
                    else if (partitionKeyProperties.NameEquals(JsonEncodedStrings.SystemKey.EncodedUtf8Bytes))
                    {
                        setting.PartitionKey.IsSystemKey = partitionKeyProperties.Value.GetBoolean();
                    }
                }
            }
        }
    }
}
