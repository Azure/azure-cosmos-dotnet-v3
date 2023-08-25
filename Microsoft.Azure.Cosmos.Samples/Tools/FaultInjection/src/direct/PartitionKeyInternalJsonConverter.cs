//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Documents.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal sealed class PartitionKeyInternalJsonConverter : JsonConverter
    {
        private const string Type = "type";
        private const string MinNumber = "MinNumber";
        private const string MaxNumber = "MaxNumber";
        private const string MinString = "MinString";
        private const string MaxString = "MaxString";
        private const string Infinity = "Infinity";

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            PartitionKeyInternal partitionKey = (PartitionKeyInternal)value;

            if (partitionKey.Equals(PartitionKeyInternal.ExclusiveMaximum))
            {
                writer.WriteValue(Infinity);
                return;
            }

            writer.WriteStartArray();

            foreach (IPartitionKeyComponent componentValue in partitionKey.Components)
            {
                componentValue.JsonEncode(writer);
            }

            writer.WriteEndArray();
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            if (token.Type == JTokenType.String && token.Value<string>() == Infinity)
            {
                return PartitionKeyInternal.ExclusiveMaximum;
            }

            List<object> values = new List<object>();

            if (token.Type == JTokenType.Array)
            {
                foreach (JToken item in ((JArray)token))
                {
                    if (item is JObject)
                    {
                        JObject obj = (JObject)item;

                        if (!obj.Properties().Any())
                        {
                            values.Add(Undefined.Value);
                        }
                        else
                        {
                            bool valid = false;
                            JToken val;

                            if (obj.TryGetValue(Type, out val))
                            {
                                if (val.Type == JTokenType.String)
                                {
                                    valid = true;

                                    if (val.Value<string>() == MinNumber)
                                    {
                                        values.Add(Microsoft.Azure.Documents.Routing.MinNumber.Value);
                                    }
                                    else if (val.Value<string>() == MaxNumber)
                                    {
                                        values.Add(Microsoft.Azure.Documents.Routing.MaxNumber.Value);
                                    }
                                    else if (val.Value<string>() == MinString)
                                    {
                                        values.Add(Microsoft.Azure.Documents.Routing.MinString.Value);
                                    }
                                    else if (val.Value<string>() == MaxString)
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
                                throw new JsonSerializationException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, token));
                            }
                        }
                    }
                    else if (item is JValue)
                    {
                        values.Add(((JValue)item).Value);
                    }
                    else
                    {
                        throw new JsonSerializationException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, token));
                    }
                }

                return PartitionKeyInternal.FromObjectArray(values, true);
            }

            throw new JsonSerializationException(string.Format(CultureInfo.InvariantCulture, RMResources.UnableToDeserializePartitionKeyValue, token));
        }

        public override bool CanConvert(Type objectType)
        {
            return typeof(PartitionKeyInternal).IsAssignableFrom(objectType);
        }

        public static void JsonEncode(MinNumberPartitionKeyComponent component, JsonWriter writer)
        {
            JsonEncodeLimit(writer, MinNumber);
        }

        public static void JsonEncode(MaxNumberPartitionKeyComponent component, JsonWriter writer)
        {
            JsonEncodeLimit(writer, MaxNumber);
        }

        public static void JsonEncode(MinStringPartitionKeyComponent component, JsonWriter writer)
        {
            JsonEncodeLimit(writer, MinString);
        }

        public static void JsonEncode(MaxStringPartitionKeyComponent component, JsonWriter writer)
        {
            JsonEncodeLimit(writer, MaxString);
        }

        private static void JsonEncodeLimit(JsonWriter writer, string value)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(Type);

            writer.WriteValue(value);

            writer.WriteEndObject();
        }
    }
}
