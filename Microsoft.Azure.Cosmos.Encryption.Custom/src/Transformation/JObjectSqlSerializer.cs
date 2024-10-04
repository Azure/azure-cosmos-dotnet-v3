// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Diagnostics;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal static class JObjectSqlSerializer
    {
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

        // UTF-8 encoding.
        private static readonly SqlVarCharSerializer SqlVarCharSerializer = new (size: -1, codePageCharacterEncoding: 65001);

        private static readonly JsonSerializerSettings JsonSerializerSettings = EncryptionProcessor.JsonSerializerSettings;

        internal static (TypeMarker typeMarker, byte[] serializedBytes, int serializedBytesCount) Serialize(JToken propertyValue, ArrayPoolManager arrayPoolManager)
        {
            byte[] buffer;
            int length;
            switch (propertyValue.Type)
            {
                case JTokenType.Undefined:
                    Debug.Assert(false, "Undefined value cannot be in the JSON");
                    return (default, null, -1);
                case JTokenType.Null:
                    Debug.Assert(false, "Null type should have been handled by caller");
                    return (TypeMarker.Null, null, -1);
                case JTokenType.Boolean:
                    (buffer, length) = SerializeFixed(SqlBoolSerializer);
                    return (TypeMarker.Boolean, buffer, length);
                case JTokenType.Float:
                    (buffer, length) = SerializeFixed(SqlDoubleSerializer);
                    return (TypeMarker.Double, buffer, length);
                case JTokenType.Integer:
                    (buffer, length) = SerializeFixed(SqlLongSerializer);
                    return (TypeMarker.Long, buffer, length);
                case JTokenType.String:
                    (buffer, length) = SerializeString(propertyValue.ToObject<string>());
                    return (TypeMarker.String, buffer, length);
                case JTokenType.Array:
                    (buffer, length) = SerializeString(propertyValue.ToString());
                    return (TypeMarker.Array, buffer, length);
                case JTokenType.Object:
                    (buffer, length) = SerializeString(propertyValue.ToString());
                    return (TypeMarker.Object, buffer, length);
                default:
                    throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {propertyValue.Type}");
            }

            (byte[], int) SerializeFixed<T>(IFixedSizeSerializer<T> serializer)
            {
                byte[] buffer = arrayPoolManager.Rent(serializer.GetSerializedMaxByteCount());
                int length = serializer.Serialize(propertyValue.ToObject<T>(), buffer);
                return (buffer, length);
            }

            (byte[], int) SerializeString(string value)
            {
                byte[] buffer = arrayPoolManager.Rent(SqlVarCharSerializer.GetSerializedMaxByteCount(value.Length));
                int length = SqlVarCharSerializer.Serialize(value, buffer);
                return (buffer, length);
            }
        }

        internal static void DeserializeAndAddProperty(
            TypeMarker typeMarker,
            ReadOnlySpan<byte> serializedBytes,
            JObject jObject,
            string key)
        {
            switch (typeMarker)
            {
                case TypeMarker.Boolean:
                    jObject[key] = SqlBoolSerializer.Deserialize(serializedBytes);
                    break;
                case TypeMarker.Double:
                    jObject[key] = SqlDoubleSerializer.Deserialize(serializedBytes);
                    break;
                case TypeMarker.Long:
                    jObject[key] = SqlLongSerializer.Deserialize(serializedBytes);
                    break;
                case TypeMarker.String:
                    jObject[key] = SqlVarCharSerializer.Deserialize(serializedBytes);
                    break;
                case TypeMarker.Array:
                    DeserializeAndAddProperty<JArray>(serializedBytes);
                    break;
                case TypeMarker.Object:
                    DeserializeAndAddProperty<JObject>(serializedBytes);
                    break;
                default:
                    Debug.Fail(string.Format("Unexpected type marker {0}", typeMarker));
                    break;
            }

            void DeserializeAndAddProperty<T>(ReadOnlySpan<byte> serializedBytes)
                where T : JToken
            {
                using ArrayPoolManager<char> manager = new ();

                char[] buffer = manager.Rent(SqlVarCharSerializer.GetDeserializedMaxLength(serializedBytes.Length));
                int length = SqlVarCharSerializer.Deserialize(serializedBytes, buffer.AsSpan());

                JsonSerializer serializer = JsonSerializer.Create(JsonSerializerSettings);

                using MemoryTextReader memoryTextReader = new (new Memory<char>(buffer, 0, length));
                using JsonTextReader reader = new (memoryTextReader);

                jObject[key] = serializer.Deserialize<T>(reader);
            }
        }
    }
}
