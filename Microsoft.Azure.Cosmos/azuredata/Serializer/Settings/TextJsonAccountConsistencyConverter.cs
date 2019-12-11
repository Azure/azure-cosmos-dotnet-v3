//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;

    internal class TextJsonAccountConsistencyConverter : JsonConverter<AccountConsistency>
    {
        public override AccountConsistency Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonAccountConsistencyConverter.ReadProperty(root);
        }

        public override void Write(Utf8JsonWriter writer, AccountConsistency setting, JsonSerializerOptions options)
        {
            TextJsonAccountConsistencyConverter.WritePropertyValue(writer, setting, options);
        }

        public static void WritePropertyValue(Utf8JsonWriter writer, AccountConsistency setting, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WritePropertyName(Constants.Properties.DefaultConsistencyLevel);
            writer.WriteStringValue(JsonSerializer.Serialize(setting.DefaultConsistencyLevel, options));
            writer.WriteNumber(Constants.Properties.MaxStalenessPrefix, setting.MaxStalenessPrefix);
            writer.WriteNumber(Constants.Properties.MaxStalenessIntervalInSeconds, setting.MaxStalenessIntervalInSeconds);
            writer.WriteEndObject();
        }

        public static AccountConsistency ReadProperty(JsonElement root)
        {
            AccountConsistency setting = new AccountConsistency();
            foreach (JsonProperty property in root.EnumerateObject())
            {
                TextJsonAccountConsistencyConverter.ReadPropertyValue(setting, property);
            }

            return setting;
        }

        private static void ReadPropertyValue(AccountConsistency setting, JsonProperty property)
        {
            if (property.NameEquals(Constants.Properties.MaxStalenessPrefix))
            {
                setting.MaxStalenessPrefix = property.Value.GetInt32();
            }
            else if (property.NameEquals(Constants.Properties.MaxStalenessIntervalInSeconds))
            {
                setting.MaxStalenessIntervalInSeconds = property.Value.GetInt32();
            }
            else if (property.NameEquals(Constants.Properties.DefaultConsistencyLevel))
            {
                if (Enum.TryParse<ConsistencyLevel>(property.Value.GetString(), out ConsistencyLevel consistencyLevel))
                {
                    setting.DefaultConsistencyLevel = consistencyLevel;
                }
            }
        }
    }
}
