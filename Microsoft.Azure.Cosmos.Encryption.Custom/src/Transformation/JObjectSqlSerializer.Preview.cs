// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Diagnostics;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class JObjectSqlSerializer
    {
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

        // UTF-8 encoding.
        private static readonly SqlVarCharSerializer SqlVarCharSerializer = new (size: -1, codePageCharacterEncoding: 65001);

        private static readonly JsonSerializerSettings JsonSerializerSettings = EncryptionProcessor.JsonSerializerSettings;

#pragma warning disable SA1101 // Prefix local calls with this - false positive on SerializeFixed
        internal virtual (TypeMarker typeMarker, byte[] serializedBytes, int serializedBytesCount) Serialize(JToken propertyValue, ArrayPoolManager arrayPoolManager)
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
                    (buffer, length) = SerializeString(propertyValue.ToString(Formatting.None));
                    return (TypeMarker.Array, buffer, length);
                case JTokenType.Object:
                    (buffer, length) = SerializeString(propertyValue.ToString(Formatting.None));
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

        internal virtual void DeserializeAndAddProperty(
            TypeMarker typeMarker,
            ReadOnlySpan<byte> serializedBytes,
            JObject jObject,
            string key,
            ArrayPoolManager<char> arrayPoolManager)
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
                    jObject[key] = Deserialize<JArray>(serializedBytes);
                    break;
                case TypeMarker.Object:
                    jObject[key] = Deserialize<JObject>(serializedBytes);
                    break;
                default:
                    Debug.Fail(string.Format("Unexpected type marker {0}", typeMarker));
                    break;
            }

            T Deserialize<T>(ReadOnlySpan<byte> serializedBytes)
                where T : JToken
            {
                char[] buffer = arrayPoolManager.Rent(SqlVarCharSerializer.GetDeserializedMaxLength(serializedBytes.Length));
                int length = SqlVarCharSerializer.Deserialize(serializedBytes, buffer.AsSpan());

                JsonSerializer serializer = JsonSerializer.Create(JsonSerializerSettings);

                using MemoryTextReader memoryTextReader = new (new Memory<char>(buffer, 0, length));
                using JsonTextReader reader = new (memoryTextReader);

                return serializer.Deserialize<T>(reader);
            }
        }
#pragma warning restore SA1101 // Prefix local calls with this
    }
}

#endif