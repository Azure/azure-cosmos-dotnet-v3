//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonConflictResolutionPolicyConverter : JsonConverter<ConflictResolutionPolicy>
    {
        public override ConflictResolutionPolicy Read(
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
            return TextJsonConflictResolutionPolicyConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ConflictResolutionPolicy policy,
            JsonSerializerOptions options)
        {
            TextJsonConflictResolutionPolicyConverter.WritePropertyValues(writer, policy, options);
        }

        public static void WritePropertyValues(
            Utf8JsonWriter writer,
            ConflictResolutionPolicy policy,
            JsonSerializerOptions options)
        {
            if (policy == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteString(Constants.Properties.Mode, policy.Mode.ToString());

            if (!string.IsNullOrEmpty(policy.ResolutionPath))
            {
                writer.WriteString(Constants.Properties.ConflictResolutionPath, policy.ResolutionPath);
            }

            if (!string.IsNullOrEmpty(policy.ResolutionProcedure))
            {
                writer.WriteString(Constants.Properties.ConflictResolutionProcedure, policy.ResolutionProcedure);
            }

            writer.WriteEndObject();
        }

        public static ConflictResolutionPolicy ReadProperty(JsonElement root)
        {
            ConflictResolutionPolicy policy = new ConflictResolutionPolicy();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonConflictResolutionPolicyConverter.ReadPropertyValue(policy, property);
            }

            return policy;
        }

        private static void ReadPropertyValue(
            ConflictResolutionPolicy policy,
            JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.Mode))
            {
                if (Enum.TryParse<ConflictResolutionMode>(property.Value.GetString(), out ConflictResolutionMode conflictResolutionMode))
                {
                    policy.Mode = conflictResolutionMode;
                }
            }
            else if (property.NameEquals(Constants.Properties.ConflictResolutionPath))
            {
                policy.ResolutionPath = property.Value.GetString();
            }
            else if (property.NameEquals(Constants.Properties.ConflictResolutionProcedure))
            {
                policy.ResolutionProcedure = property.Value.GetString();
            }
        }
    }
}
