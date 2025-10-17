// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Resource.FullFidelity.Converters
{
    using System;
    using System.Collections.Generic;
    using Newtonsoft.Json;

    internal class ListNewtonSoftConverter : JsonConverter
    {
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType != JsonToken.StartObject)
            {
                throw new JsonException($"Unexpected token parsing PartitionKey. Expected StartObject, got {reader.TokenType}.");
            }

            List<(string, object)> partitionKey = new List<(string, object)>();

            reader.Read(); // Move to the first property in the object
            while (reader.TokenType == JsonToken.PropertyName)
            {
                string key = reader.Value.ToString();
                reader.Read(); // Move to the value of the property

                object value = reader.TokenType switch
                {
                    JsonToken.String => reader.Value.ToString(),
                    JsonToken.Integer => Convert.ToInt64(reader.Value),
                    JsonToken.Float => Convert.ToDouble(reader.Value),
                    JsonToken.Boolean => Convert.ToBoolean(reader.Value),
                    JsonToken.Null => null,
                    _ => throw new JsonSerializationException($"Unexpected token type: {reader.TokenType} for PartitionKey property.")
                };

                partitionKey.Add((key, value));
                reader.Read(); // Move to the next property or EndObject
            }
            return partitionKey;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is not List<(string, object)>)
            {
                throw new JsonException($"Unexpected value when converting {nameof(List<(string, object)>)}.");
            }
            writer.WriteStartObject();

            foreach ((string key, object val) in (List<(string, object)>)value)
            {
                writer.WritePropertyName(key);

                if (val == null)
                {
                    writer.WriteNull();
                }
                else
                {
                    switch (val)
                    {
                        case string stringValue:
                            writer.WriteValue(stringValue);
                            break;

                        case long longValue:
                            writer.WriteValue(longValue);
                            break;

                        case double doubleValue:
                            writer.WriteValue(doubleValue);
                            break;

                        case bool boolValue:
                            writer.WriteValue(boolValue);
                            break;

                        default:
                            throw new JsonException($"Unexpected value type '{val}' for key '{key}'.");
                    }
                }
            }

            writer.WriteEndObject();
        }

        /// <summary>
        /// Determines whether this instance can convert the specified object type.
        /// </summary>
        /// <param name="objectType">Type of the object.</param>
        /// <returns><c>true</c> if this instance can convert the specified object type; otherwise, <c>false</c>.</returns>
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(List<(string, object)>);
        }
    }
}
