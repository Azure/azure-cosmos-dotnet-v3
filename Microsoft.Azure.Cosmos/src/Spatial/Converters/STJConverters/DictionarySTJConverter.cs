// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Spatial.Converters.STJConverters
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    // System.Text.Json by default returns JsonElement as a value type for any key in the dictionary as it cant infer the type of value . This Converter is required to translate JsonElement to .NET type to return back to the client.
    internal class DictionarySTJConverter : JsonConverter<IDictionary<string, object>>
    {
        public override IDictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(RMResources.JsonUnexpectedToken);
            }
            Dictionary<string, object> dictionary = new Dictionary<string, object>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return dictionary;
                }
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException(RMResources.JsonUnexpectedToken);
                }
                string propertyName = reader.GetString();
                reader.Read(); // get the value
                object value = this.getValue(ref reader, options);
                dictionary.Add(propertyName, value);
            }

            return dictionary;

        }
        public override void Write(Utf8JsonWriter writer, IDictionary<string, object> dict, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, (Dictionary<string, object>)dict, options);

        }

        private object getValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
        {
            return reader.TokenType switch
            {
                JsonTokenType.String when reader.TryGetDateTime(out DateTime datetime) => datetime,
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.True => true,
                JsonTokenType.False => false,
                JsonTokenType.Number when reader.TryGetInt32(out int i) => i,
                JsonTokenType.Number when reader.TryGetInt64(out long l) => l,
                JsonTokenType.Number => reader.GetDouble(),
                JsonTokenType.Null => null,
                JsonTokenType.StartObject => this.Read(ref reader, null!, options),
                _ => throw new JsonException(RMResources.JsonUnexpectedToken)
            };
        }

    }
}