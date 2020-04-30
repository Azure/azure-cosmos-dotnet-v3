// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Json
{
    using System;
    using System.Collections;
    using System.Reflection;
    using Microsoft.Azure.Cosmos.CosmosElements;

    internal static class Serializer
    {
        public static ReadOnlyMemory<byte> Serialize(
            object value,
            JsonSerializationFormat jsonSerializationFormat = JsonSerializationFormat.Text)
        {
            IJsonWriter jsonWriter = JsonWriter.Create(jsonSerializationFormat, skipValidation: false);
            Serializer.SerializeInternal(value, jsonWriter);
            return jsonWriter.GetResult();
        }

        public static void SerializeInternal(
            object value,
            IJsonWriter jsonWriter)
        {
            if (jsonWriter == null)
            {
                throw new ArgumentNullException(nameof(jsonWriter));
            }

            switch (value)
            {
                case null:
                    jsonWriter.WriteNullValue();
                    break;

                case bool boolValue:
                    jsonWriter.WriteBoolValue(boolValue);
                    break;

                case string stringValue:
                    jsonWriter.WriteStringValue(stringValue);
                    break;

                case Number64 numberValue:
                    jsonWriter.WriteNumber64Value(numberValue);
                    break;

                case sbyte signedByteValue:
                    jsonWriter.WriteInt8Value(signedByteValue);
                    break;

                case short shortValue:
                    jsonWriter.WriteInt16Value(shortValue);
                    break;

                case int intValue:
                    jsonWriter.WriteInt32Value(intValue);
                    break;

                case long longValue:
                    jsonWriter.WriteInt64Value(longValue);
                    break;

                case uint uintValue:
                    jsonWriter.WriteUInt32Value(uintValue);
                    break;

                case float floatValue:
                    jsonWriter.WriteFloat32Value(floatValue);
                    break;

                case double doubleValue:
                    jsonWriter.WriteFloat64Value(doubleValue);
                    break;

                case ReadOnlyMemory<byte> binaryValue:
                    jsonWriter.WriteBinaryValue(binaryValue.Span);
                    break;

                case Guid guidValue:
                    jsonWriter.WriteGuidValue(guidValue);
                    break;

                case IEnumerable enumerableValue:
                    jsonWriter.WriteArrayStart();

                    foreach (object arrayItem in enumerableValue)
                    {
                        Serializer.SerializeInternal(arrayItem, jsonWriter);
                    }

                    jsonWriter.WriteArrayEnd();
                    break;

                case CosmosElement cosmosElementValue:
                    cosmosElementValue.WriteTo(jsonWriter);
                    break;

                case ValueType valueType:
                    throw new ArgumentOutOfRangeException($"Unable to serialize type: {valueType.GetType()}");

                default:
                    Type type = value.GetType();
                    PropertyInfo[] properties = type.GetProperties();

                    jsonWriter.WriteObjectStart();

                    foreach (PropertyInfo propertyInfo in properties)
                    {
                        jsonWriter.WriteFieldName(propertyInfo.Name);
                        object propertyValue = propertyInfo.GetValue(value);
                        Serializer.SerializeInternal(propertyValue, jsonWriter);
                    }

                    jsonWriter.WriteObjectEnd();
                    break;
            }
        }
    }
}
