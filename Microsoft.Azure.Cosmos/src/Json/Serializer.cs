// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections;
    using System.Reflection;

    internal static class Serializer
    {
        public static ReadOnlyMemory<byte> Serialize(
            object poco,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: false);
            Serializer.SerializeInternal(poco, jsonWriter);
            return jsonWriter.GetResult();
        }

        private static void SerializeInternal(
            object poco,
            IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            switch (poco)
            {
                case null:
                    jsonWriter.WriteNullValue();
                    break;

                case bool value:
                    jsonWriter.WriteBoolValue(value);
                    break;

                case string value:
                    jsonWriter.WriteStringValue(value);
                    break;

                case Number64 value:
                    jsonWriter.WriteNumberValue(value);
                    break;

                case sbyte value:
                    jsonWriter.WriteInt8Value(value);
                    break;

                case short value:
                    jsonWriter.WriteInt16Value(value);
                    break;

                case int value:
                    jsonWriter.WriteInt32Value(value);
                    break;

                case long value:
                    jsonWriter.WriteInt64Value(value);
                    break;

                case uint value:
                    jsonWriter.WriteUInt32Value(value);
                    break;

                case float value:
                    jsonWriter.WriteFloat32Value(value);
                    break;

                case double value:
                    jsonWriter.WriteFloat64Value(value);
                    break;

                case ReadOnlyMemory<byte> value:
                    jsonWriter.WriteBinaryValue(value.Span);
                    break;

                case Guid value:
                    jsonWriter.WriteGuidValue(value);
                    break;

                case IEnumerable value:
                    jsonWriter.WriteArrayStart();

                    foreach (object arrayItem in value)
                    {
                        Serializer.SerializeInternal(arrayItem, jsonWriter);
                    }

                    jsonWriter.WriteArrayEnd();
                    break;

                case ValueType valueType:
                    throw new ArgumentOutOfRangeException($"Unable to serialize type: {valueType.GetType()}");

                default:
                    Type type = poco.GetType();
                    PropertyInfo[] properties = type.GetProperties();

                    jsonWriter.WriteObjectStart();

                    foreach (PropertyInfo propertyInfo in properties)
                    {
                        jsonWriter.WriteFieldName(propertyInfo.Name);
                        object propertyValue = propertyInfo.GetValue(poco);
                        Serializer.SerializeInternal(propertyValue, jsonWriter);
                    }

                    jsonWriter.WriteObjectEnd();
                    break;
            }
        }

        public static ReadOnlyMemory<byte> Serialize(
            bool value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteBoolValue(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            string value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteStringValue(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            Number64 value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteNumberValue(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            sbyte value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteInt8Value(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            short value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteInt16Value(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            int value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteInt32Value(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            long value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteInt64Value(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            uint value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteUInt32Value(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            float value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteFloat32Value(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            double value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteFloat64Value(value);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            ReadOnlyMemory<byte> value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteBinaryValue(value.Span);
            return jsonWriter.GetResult();
        }

        public static ReadOnlyMemory<byte> Serialize(
            Guid value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: true);
            jsonWriter.WriteGuidValue(value);
            return jsonWriter.GetResult();
        }
    }
}
