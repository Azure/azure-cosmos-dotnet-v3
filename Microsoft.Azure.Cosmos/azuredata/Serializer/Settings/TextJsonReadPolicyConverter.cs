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

    internal class TextJsonReadPolicyConverter : JsonConverter<ReadPolicy>
    {
        public override ReadPolicy Read(
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
            return TextJsonReadPolicyConverter.ReadProperty(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            ReadPolicy policy,
            JsonSerializerOptions options)
        {
            TextJsonReadPolicyConverter.WritePropertyValue(writer, policy);
        }

        public static void WritePropertyValue(
            Utf8JsonWriter writer,
            ReadPolicy policy)
        {
            if (policy == null)
            {
                return;
            }

            writer.WriteStartObject();

            writer.WriteNumber(JsonEncodedStrings.PrimaryReadCoefficient, policy.PrimaryReadCoefficient);

            writer.WriteNumber(JsonEncodedStrings.SecondaryReadCoefficient, policy.SecondaryReadCoefficient);

            writer.WriteEndObject();
        }

        public static ReadPolicy ReadProperty(JsonElement root)
        {
            ReadPolicy policy = new ReadPolicy();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonReadPolicyConverter.ReadPropertyValue(policy, property);
            }

            return policy;
        }

        private static void ReadPropertyValue(
            ReadPolicy policy,
            JsonProperty property)
        {
            if (property.NameEquals(JsonEncodedStrings.PrimaryReadCoefficient.EncodedUtf8Bytes))
            {
                policy.PrimaryReadCoefficient = property.Value.GetInt32();
            }
            else if (property.NameEquals(JsonEncodedStrings.SecondaryReadCoefficient.EncodedUtf8Bytes))
            {
                policy.SecondaryReadCoefficient = property.Value.GetInt32();
            }
        }
    }
}
