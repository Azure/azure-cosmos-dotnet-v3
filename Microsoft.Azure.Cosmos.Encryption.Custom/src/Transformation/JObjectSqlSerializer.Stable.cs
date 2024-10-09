// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

#if !ENCRYPTION_CUSTOM_PREVIEW

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Transformation
{
    using System;
    using System.Diagnostics;
    using Microsoft.Data.Encryption.Cryptography.Serializers;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    internal class JObjectSqlSerializer
    {
        private static readonly SqlSerializerFactory SqlSerializerFactory = new ();

        // UTF-8 encoding.
        private static readonly SqlVarCharSerializer SqlVarCharSerializer = new (size: -1, codePageCharacterEncoding: 65001);

        private static readonly JsonSerializerSettings JsonSerializerSettings = EncryptionProcessor.JsonSerializerSettings;

        internal virtual (TypeMarker, byte[]) Serialize(JToken propertyValue)
        {
            switch (propertyValue.Type)
            {
                case JTokenType.Undefined:
                    Debug.Assert(false, "Undefined value cannot be in the JSON");
                    return (default, null);
                case JTokenType.Null:
                    Debug.Assert(false, "Null type should have been handled by caller");
                    return (TypeMarker.Null, null);
                case JTokenType.Boolean:
                    return (TypeMarker.Boolean, SqlSerializerFactory.GetDefaultSerializer<bool>().Serialize(propertyValue.ToObject<bool>()));
                case JTokenType.Float:
                    return (TypeMarker.Double, SqlSerializerFactory.GetDefaultSerializer<double>().Serialize(propertyValue.ToObject<double>()));
                case JTokenType.Integer:
                    return (TypeMarker.Long, SqlSerializerFactory.GetDefaultSerializer<long>().Serialize(propertyValue.ToObject<long>()));
                case JTokenType.String:
                    return (TypeMarker.String, SqlVarCharSerializer.Serialize(propertyValue.ToObject<string>()));
                case JTokenType.Array:
                    return (TypeMarker.Array, SqlVarCharSerializer.Serialize(propertyValue.ToString()));
                case JTokenType.Object:
                    return (TypeMarker.Object, SqlVarCharSerializer.Serialize(propertyValue.ToString()));
                default:
                    throw new InvalidOperationException($" Invalid or Unsupported Data Type Passed : {propertyValue.Type}");
            }
        }

        internal virtual void DeserializeAndAddProperty(
            TypeMarker typeMarker,
            byte[] serializedBytes,
            JObject jObject,
            string key)
        {
            switch (typeMarker)
            {
                case TypeMarker.Boolean:
                    jObject[key] = SqlSerializerFactory.GetDefaultSerializer<bool>().Deserialize(serializedBytes);
                    break;
                case TypeMarker.Double:
                    jObject[key] = SqlSerializerFactory.GetDefaultSerializer<double>().Deserialize(serializedBytes);
                    break;
                case TypeMarker.Long:
                    jObject[key] = SqlSerializerFactory.GetDefaultSerializer<long>().Deserialize(serializedBytes);
                    break;
                case TypeMarker.String:
                    jObject[key] = SqlVarCharSerializer.Deserialize(serializedBytes);
                    break;
                case TypeMarker.Array:
                    jObject[key] = JsonConvert.DeserializeObject<JArray>(SqlVarCharSerializer.Deserialize(serializedBytes), JsonSerializerSettings);
                    break;
                case TypeMarker.Object:
                    jObject[key] = JsonConvert.DeserializeObject<JObject>(SqlVarCharSerializer.Deserialize(serializedBytes), JsonSerializerSettings);
                    break;
                default:
                    Debug.Fail(string.Format("Unexpected type marker {0}", typeMarker));
                    break;
            }
        }
    }
}

#endif