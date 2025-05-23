//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    internal sealed class PartitionKeyInternalJsonConverter : JsonConverter<PartitionKeyInternal>
    {
        private const string TypePropertyName = "type";
        private const string MinNumber = "MinNumber";
        private const string MaxNumber = "MaxNumber";
        private const string MinString = "MinString";
        private const string MaxString = "MaxString";
        private const string Infinity = "Infinity";

        public override void Write(Utf8JsonWriter writer, PartitionKeyInternal partitionKey, JsonSerializerOptions options)
        {
            if (partitionKey.Equals(PartitionKeyInternal.ExclusiveMaximum))
            {
                writer.WriteStringValue(Infinity);
                return;
            }

            writer.WriteStartArray();

            IEnumerable<IPartitionKeyComponent> components = partitionKey.Components ?? Enumerable.Empty<IPartitionKeyComponent>();
            foreach (IPartitionKeyComponent componentValue in components)
            {
                componentValue.JsonEncode(writer);
            }

            writer.WriteEndArray();
        }

        public override PartitionKeyInternal Read(
            ref Utf8JsonReader reader,
            Type objectType,
            JsonSerializerOptions options)
        {
            JsonDocument document = JsonDocument.ParseValue(ref reader);
            JsonElement root = document.RootElement;
            
            if (root.ValueKind == JsonValueKind.String && root.GetString() == Infinity)
            {
                return PartitionKeyInternal.ExclusiveMaximum;
            }

            List<object> values = new List<object>();
            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        if (item.EnumerateObject().Count() == 0)
                        {
                            values.Add(Undefined.Value);
                        }
                        else
                        {
                            bool valid = false;

                            if (item.TryGetProperty(TypePropertyName, out JsonElement property))
                            {
                                if (property.ValueKind == JsonValueKind.String)
                                {
                                    string val = property.GetString();
                                    valid = true;

                                    if (val == MinNumber)
                                    {
                                        values.Add(Microsoft.Azure.Documents.Routing.MinNumber.Value);
                                    }
                                    else if (val == MaxNumber)
                                    {
                                        values.Add(Microsoft.Azure.Documents.Routing.MaxNumber.Value);
                                    }
                                    else if (val == MinString)
                                    {
                                        values.Add(Microsoft.Azure.Documents.Routing.MinString.Value);
                                    }
                                    else if (val == MaxString)
                                    {
                                        values.Add(Microsoft.Azure.Documents.Routing.MaxString.Value);
                                    }
                                    else
                                    {
                                        valid = false;
                                    }
                                }
                            }

                            if (!valid)
                            {
                                throw new JsonException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, root.ToString()));
                            }
                        }
                    }
                    else
                    {
                        object value = item.ValueKind switch
                        {
                            JsonValueKind.String => item.GetString(),
                            JsonValueKind.Number => item.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => throw new JsonException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, root.ToString()))
                        };
                        values.Add(value);
                    }
                }

                return PartitionKeyInternal.FromObjectArray(values, true);
            }

            throw new JsonException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, root.ToString()));
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(PartitionKeyInternal).IsAssignableFrom(objectType);
        }

        public static void JsonEncode(MinNumberPartitionKeyComponent component, Utf8JsonWriter writer)
        {
            JsonEncodeLimit(writer, MinNumber);
        }

        public static void JsonEncode(MaxNumberPartitionKeyComponent component, Utf8JsonWriter writer)
        {
            JsonEncodeLimit(writer, MaxNumber);
        }

        public static void JsonEncode(MinStringPartitionKeyComponent component, Utf8JsonWriter writer)
        {
            JsonEncodeLimit(writer, MinString);
        }

        public static void JsonEncode(MaxStringPartitionKeyComponent component, Utf8JsonWriter writer)
        {
            JsonEncodeLimit(writer, MaxString);
        }

        private static void JsonEncodeLimit(Utf8JsonWriter writer, string value)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(TypePropertyName);

            writer.WriteStringValue(value);

            writer.WriteEndObject();
        }
    }
}
