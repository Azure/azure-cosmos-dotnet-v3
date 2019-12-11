//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonReadPolicyConverter : JsonConverter<ReadPolicy>
    {
        public override ReadPolicy Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonReadPolicyConverter.ReadProperty(root);
        }

        public override void Write(Utf8JsonWriter writer, ReadPolicy policy, JsonSerializerOptions options)
        {
            TextJsonReadPolicyConverter.WritePropertyValue(writer, policy);
        }

        public static void WritePropertyValue(Utf8JsonWriter writer, ReadPolicy policy)
        {
            writer.WriteStartObject();
            writer.WriteNumber(Constants.Properties.PrimaryReadCoefficient, policy.PrimaryReadCoefficient);
            writer.WriteNumber(Constants.Properties.SecondaryReadCoefficient, policy.SecondaryReadCoefficient);
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

        private static void ReadPropertyValue(ReadPolicy policy, JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.PrimaryReadCoefficient))
            {
                policy.PrimaryReadCoefficient = property.Value.GetInt32();
            }
            else if (property.NameEquals(Constants.Properties.SecondaryReadCoefficient))
            {
                policy.SecondaryReadCoefficient = property.Value.GetInt32();
            }
        }
    }
}
