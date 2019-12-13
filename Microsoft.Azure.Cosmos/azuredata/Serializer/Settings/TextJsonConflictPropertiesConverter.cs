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

    internal class TextJsonConflictPropertiesConverter : JsonConverter<ConflictProperties>
    {
        public override ConflictProperties Read(
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
            return TextJsonConflictPropertiesConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ConflictProperties settings,
            JsonSerializerOptions options)
        {
            TextJsonConflictPropertiesConverter.WritePropertyValues(writer, settings, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            ConflictProperties settings,
            JsonSerializerOptions options)
        {
            if (settings == null)
            {
                return;
            }

            writer.WriteStartObject();
            writer.WriteString(Constants.Properties.Id, settings.Id);

            writer.WritePropertyName(Constants.Properties.OperationType);
            writer.WriteStringValue(JsonSerializer.Serialize(settings.OperationKind, options));

            writer.WriteString(Constants.Properties.ResourceType, TextJsonConflictPropertiesConverter.ParseResourceType(settings.ResourceType));

            writer.WriteString(Constants.Properties.SourceResourceId, settings.SourceResourceId);

            writer.WriteString(Constants.Properties.Content, settings.Content);

            writer.WriteNumber(Constants.Properties.ConflictLSN, settings.ConflictLSN);

            writer.WriteEndObject();
        }

        public static ConflictProperties ReadProperty(JsonElement root)
        {
            ConflictProperties settings = new ConflictProperties();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonConflictPropertiesConverter.ReadPropertyValue(settings, property);
            }

            return settings;
        }

        private static void ReadPropertyValue(
            ConflictProperties settings,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Id))
            {
                settings.Id = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.OperationType))
            {
                if (Enum.TryParse<OperationKind>(property.Value.GetString(), out OperationKind operationKind))
                {
                    settings.OperationKind = operationKind;
                }
            }
            else if (property.NameEquals(Constants.Properties.ResourceType))
            {
                settings.ResourceType = TextJsonConflictPropertiesConverter.ParseResourceType(property.Value.GetString());
            }
            else if (property.NameEquals(Constants.Properties.SourceResourceId))
            {
                settings.SourceResourceId = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.Content))
            {
                settings.Content = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.ConflictLSN))
            {
                settings.ConflictLSN = property.Value.GetInt64();
            }
        }

        private static Type ParseResourceType(string resourceType)
        {
            if (string.Equals(Constants.Properties.ResourceTypeDocument, resourceType, StringComparison.OrdinalIgnoreCase))
            {
                return typeof(Microsoft.Azure.Cosmos.CosmosElements.CosmosElement);
            }
            else if (string.Equals(Constants.Properties.ResourceTypeStoredProcedure, resourceType, StringComparison.OrdinalIgnoreCase))
            {
                return typeof(StoredProcedureProperties);
            }
            else if (string.Equals(Constants.Properties.ResourceTypeTrigger, resourceType, StringComparison.OrdinalIgnoreCase))
            {
                return typeof(TriggerProperties);
            }
            else if (string.Equals(Constants.Properties.ResourceTypeUserDefinedFunction, resourceType, StringComparison.OrdinalIgnoreCase))
            {
                return typeof(UserDefinedFunctionProperties);
            }

            return null;
        }

        private static string ParseResourceType(Type valueAsType)
        {
            if (valueAsType == typeof(Microsoft.Azure.Cosmos.CosmosElements.CosmosElement))
            {
                return Constants.Properties.ResourceTypeDocument;
            }
            else if (valueAsType == typeof(StoredProcedureProperties))
            {
                return Constants.Properties.ResourceTypeStoredProcedure;
            }
            else if (valueAsType == typeof(TriggerProperties))
            {
                return Constants.Properties.ResourceTypeTrigger;
            }
            else if (valueAsType == typeof(UserDefinedFunctionProperties))
            {
                return Constants.Properties.ResourceTypeUserDefinedFunction;
            }

            throw new ArgumentException(String.Format(CultureInfo.CurrentUICulture, "Unsupported resource type {0}", valueAsType.ToString()));
        }
    }
}
