// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Diagnostics;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Microsoft.Data.Encryption.Cryptography.Serializers;

    internal class JsonNodeSqlSerializer
    {
        private static readonly SqlBitSerializer SqlBoolSerializer = new ();
        private static readonly SqlFloatSerializer SqlDoubleSerializer = new ();
        private static readonly SqlBigIntSerializer SqlLongSerializer = new ();

        // UTF-8 encoding.
        private static readonly SqlVarCharSerializer SqlVarCharSerializer = new (size: -1, codePageCharacterEncoding: 65001);

#pragma warning disable SA1101 // Prefix local calls with this - false positive on SerializeFixed
        internal virtual (TypeMarker typeMarker, byte[] serializedBytes, int serializedBytesCount) Serialize(JsonNode propertyValue, ArrayPoolManager arrayPoolManager)
        {
            byte[] buffer;
            int length;

            if (propertyValue == null)
            {
                return (TypeMarker.Null, null, -1);
            }

            switch (propertyValue.GetValueKind())
            {
                case JsonValueKind.Undefined:
                    Debug.Assert(false, "Undefined value cannot be in the JSON");
                    return (default, null, -1);
                case JsonValueKind.Null:
                    Debug.Assert(false, "Null type should have been handled by caller");
                    return (TypeMarker.Null, null, -1);
                case JsonValueKind.True:
                    (buffer, length) = SerializeFixed(SqlBoolSerializer, true);
                    return (TypeMarker.Boolean, buffer, length);
                case JsonValueKind.False:
                    (buffer, length) = SerializeFixed(SqlBoolSerializer, false);
                    return (TypeMarker.Boolean, buffer, length);
                case JsonValueKind.Number:
                    if (long.TryParse(propertyValue.ToJsonString(), out long longValue))
                    {
                        (buffer, length) = SerializeFixed(SqlLongSerializer, longValue);
                        return (TypeMarker.Long, buffer, length);
                    }
                    else if (double.TryParse(propertyValue.ToJsonString(), out double doubleValue))
                    {
                        (buffer, length) = SerializeFixed(SqlDoubleSerializer, doubleValue);
                        return (TypeMarker.Double, buffer, length);
                    }
                    else
                    {
                        throw new InvalidOperationException("Unsupported Number type");
                    }

                case JsonValueKind.String:
                    (buffer, length) = SerializeString(propertyValue.GetValue<string>());
                    return (TypeMarker.String, buffer, length);
                case JsonValueKind.Array:
                    (buffer, length) = SerializeString(propertyValue.ToJsonString());
                    return (TypeMarker.Array, buffer, length);
                case JsonValueKind.Object:
                    (buffer, length) = SerializeString(propertyValue.ToJsonString());
                    return (TypeMarker.Object, buffer, length);
                default:
                    throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {propertyValue.GetValueKind()}");
            }

            (byte[], int) SerializeFixed<T>(IFixedSizeSerializer<T> serializer, T value)
            {
                byte[] buffer = arrayPoolManager.Rent(serializer.GetSerializedMaxByteCount());
                int length = serializer.Serialize(value, buffer);
                return (buffer, length);
            }

            (byte[], int) SerializeString(string value)
            {
                byte[] buffer = arrayPoolManager.Rent(SqlVarCharSerializer.GetSerializedMaxByteCount(value.Length));
                int length = SqlVarCharSerializer.Serialize(value, buffer);
                return (buffer, length);
            }
        }
    }
}
#endif