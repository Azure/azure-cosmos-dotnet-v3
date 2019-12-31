//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;

    internal sealed class TextJsonPartitionKeyInternalConverter : JsonConverter<PartitionKeyInternal>
    {
        private const string MinNumber = "MinNumber";
        private const string MaxNumber = "MaxNumber";
        private const string MinString = "MinString";
        private const string MaxString = "MaxString";
        private const string Infinity = "Infinity";

        public override PartitionKeyInternal Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            using JsonDocument json = JsonDocument.ParseValue(ref reader);
            JsonElement root = json.RootElement;
            return TextJsonPartitionKeyInternalConverter.ReadElement(root);
        }

        public override void Write(
            Utf8JsonWriter writer,
            PartitionKeyInternal partitionKey,
            JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public static PartitionKeyInternal ReadElement(JsonElement root)
        {
            if (root.ValueKind == JsonValueKind.String
                && root.GetString() == TextJsonPartitionKeyInternalConverter.Infinity)
            {
                return PartitionKeyInternal.ExclusiveMaximum;
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, root.GetRawText()));
            }

            List<object> values = new List<object>();
            foreach (JsonElement arrayItem in root.EnumerateArray())
            {
                if (arrayItem.ValueKind == JsonValueKind.Object)
                {
                    if (!arrayItem.EnumerateObject().Any())
                    {
                        values.Add(Undefined.Value);
                    }
                    else
                    {
                        bool valid = false;
                        if (arrayItem.TryGetProperty(JsonEncodedStrings.Type.EncodedUtf8Bytes, out JsonElement typeElement))
                        {
                            if (typeElement.ValueKind == JsonValueKind.String)
                            {
                                valid = true;

                                if (TextJsonPartitionKeyInternalConverter.MinNumber.Equals(typeElement.GetString(), StringComparison.OrdinalIgnoreCase))
                                {
                                    values.Add(Microsoft.Azure.Documents.Routing.MinNumber.Value);
                                }
                                else if (TextJsonPartitionKeyInternalConverter.MaxNumber.Equals(typeElement.GetString(), StringComparison.OrdinalIgnoreCase))
                                {
                                    values.Add(Microsoft.Azure.Documents.Routing.MaxNumber.Value);
                                }
                                else if (TextJsonPartitionKeyInternalConverter.MinString.Equals(typeElement.GetString(), StringComparison.OrdinalIgnoreCase))
                                {
                                    values.Add(Microsoft.Azure.Documents.Routing.MinString.Value);
                                }
                                else if (TextJsonPartitionKeyInternalConverter.MaxString.Equals(typeElement.GetString(), StringComparison.OrdinalIgnoreCase))
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
                            throw new JsonException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, root.GetRawText()));
                        }
                    }
                }
                else if (arrayItem.ValueKind == JsonValueKind.String)
                {
                    values.Add(arrayItem.GetString());
                }
                else
                {
                    throw new JsonException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, root.GetRawText()));
                }
            }

            return PartitionKeyInternal.FromObjectArray(values, true);
        }

        public static void WriteElement(
            Utf8JsonWriter writer,
            PartitionKeyInternal partitionKey)
        {
            if (partitionKey == null)
            {
                return;
            }

            if (partitionKey.Equals(PartitionKeyInternal.ExclusiveMaximum))
            {
                writer.WriteStringValue(TextJsonPartitionKeyInternalConverter.Infinity);
                return;
            }

            writer.WriteStartArray();

            foreach (IPartitionKeyComponent componentValue in partitionKey.Components)
            {
                // componentValue.JsonEncode(writer); TODO: Internal implementation does not support System.Text.Json writer
                TextJsonPartitionKeyInternalConverter.EncodeComponent(writer, componentValue);
            }

            writer.WriteEndArray();
        }

        private static void EncodeComponent(
            Utf8JsonWriter writer,
            IPartitionKeyComponent componentValue)
        {
            if (componentValue is StringPartitionKeyComponent)
            {
                writer.WriteStringValue((string)componentValue.ToObject()); // TODO: string value is not available to use directly
            }
            else if (componentValue is UndefinedPartitionKeyComponent)
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            else if (componentValue is NullPartitionKeyComponent)
            {
                writer.WriteNullValue();
            }
            else if (componentValue is NumberPartitionKeyComponent)
            {
                writer.WriteNumberValue((double)componentValue.ToObject());
            }
            else if (componentValue is BoolPartitionKeyComponent)
            {
                writer.WriteBooleanValue((bool)componentValue.ToObject());
            }
            else if (componentValue is MaxNumberPartitionKeyComponent)
            {
                TextJsonPartitionKeyInternalConverter.JsonEncodeLimit(writer, TextJsonPartitionKeyInternalConverter.MaxNumber);
            }
            else if (componentValue is MinStringPartitionKeyComponent)
            {
                TextJsonPartitionKeyInternalConverter.JsonEncodeLimit(writer, TextJsonPartitionKeyInternalConverter.MinString);
            }
            else if (componentValue is MaxStringPartitionKeyComponent)
            {
                TextJsonPartitionKeyInternalConverter.JsonEncodeLimit(writer, TextJsonPartitionKeyInternalConverter.MaxString);
            }
            else if (componentValue is MinNumberPartitionKeyComponent)
            {
                TextJsonPartitionKeyInternalConverter.JsonEncodeLimit(writer, TextJsonPartitionKeyInternalConverter.MinNumber);
            }
            else if (componentValue is InfinityPartitionKeyComponent)
            {
                throw new NotImplementedException();
            }
            else
            {
                throw new JsonException(string.Format(CultureInfo.CurrentCulture, RMResources.UnsupportedPartitionKeyComponentValue, componentValue));
            }
        }

        private static void JsonEncodeLimit(
            Utf8JsonWriter writer,
            string value)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(JsonEncodedStrings.Type);

            writer.WriteStringValue(value);

            writer.WriteEndObject();
        }

    }
}
