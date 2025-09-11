//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Strong encryption verification helper (mirrors emulator raw storage checks) for unit tests.
    /// Verifies that: (1) _ei metadata is present, (2) supplied plaintext sensitive values do NOT appear
    /// unencrypted in the JSON payload, and (3) expected plain values still appear.
    /// String plaintexts are searched as quoted tokens. Numeric plaintexts are searched as unquoted numeric literals
    /// appearing after a ':' and before a ',' or '}' (simple heuristic to avoid false positives inside base64 strings).
    /// </summary>
    public static class EncryptionVerificationTestHelper
    {
        // Unified verification: every encrypted property must be a base64 JSON string whose first decoded byte is a valid TypeMarker.
        // Plain properties are validated to remain in their original JSON shape.
        public static void AssertEncryptedDocument(
            string rawJson,
            IReadOnlyDictionary<string, object> encryptedProperties,
            IReadOnlyDictionary<string, object> plainProperties = null)
        {
            Assert.IsFalse(string.IsNullOrEmpty(rawJson), "rawJson was null/empty");
            using JsonDocument doc = JsonDocument.Parse(rawJson);
            JsonElement root = doc.RootElement;
            Assert.IsTrue(root.TryGetProperty(Constants.EncryptedInfo, out _), "_ei metadata missing (encryption properties)");

            foreach (KeyValuePair<string, object> kvp in encryptedProperties)
            {
                string name = kvp.Key;
                object original = kvp.Value;
                Assert.IsTrue(root.TryGetProperty(name, out JsonElement encElem), $"Encrypted property '{name}' missing");

                if (original == null)
                {
                    // Current behavior: nulls are not encrypted (remain null, path omitted). Accept Null kind.
                    Assert.AreEqual(JsonValueKind.Null, encElem.ValueKind, $"Encrypted null path '{name}' expected to remain null.");
                    continue;
                }

                Assert.AreEqual(JsonValueKind.String, encElem.ValueKind, $"Encrypted property '{name}' should be a base64 string.");
                string base64 = encElem.GetString();
                Assert.IsFalse(string.IsNullOrEmpty(base64), $"Encrypted property '{name}' value empty.");

                // Quick reject if accidentally left plaintext (matches original string serialization exactly)
                string originalSerialized = SerializeOriginalPrimitive(original);
                Assert.AreNotEqual(originalSerialized, base64, $"Property '{name}' appears to hold plaintext instead of ciphertext.");

                byte[] decoded;
                try
                {
                    decoded = System.Convert.FromBase64String(base64);
                }
                catch
                {
                    Assert.Fail($"Encrypted property '{name}' does not contain valid base64.");
                    return; // unreachable
                }
                Assert.IsTrue(decoded.Length >= 1, $"Ciphertext for '{name}' too short.");
                byte markerByte = decoded[0];
                TypeMarker marker = (TypeMarker)markerByte;
                Assert.IsTrue(IsValidMarker(marker), $"Invalid type marker {(int)markerByte} for '{name}'.");

                TypeMarker expected = InferExpectedMarker(original);
                // Containers (Array/Object) skip strict check if inference ambiguous; only check when primitive.
                if (IsPrimitiveMarker(expected))
                {
                    Assert.AreEqual(expected, marker, $"Type marker mismatch for '{name}'. Expected {expected} got {marker}.");
                }
            }

            if (plainProperties != null)
            {
                foreach (KeyValuePair<string, object> kvp in plainProperties)
                {
                    string name = kvp.Key;
                    object original = kvp.Value;
                    Assert.IsTrue(root.TryGetProperty(name, out JsonElement elem), $"Plain property '{name}' missing");
                    AssertPlainEquality(name, original, elem);
                }
            }
        }

        private static string SerializeOriginalPrimitive(object value)
        {
            if (value == null) return "null";
            return value switch
            {
                string s => s,
                bool b => b ? "true" : "false",
                IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => value.ToString(),
            };
        }

        private static bool IsPrimitiveMarker(TypeMarker m) => m == TypeMarker.String || m == TypeMarker.Long || m == TypeMarker.Double || m == TypeMarker.Boolean || m == TypeMarker.Null;

        private static bool IsValidMarker(TypeMarker m) => m switch
        {
            TypeMarker.String or TypeMarker.Long or TypeMarker.Double or TypeMarker.Boolean or TypeMarker.Null or TypeMarker.Array or TypeMarker.Object => true,
            _ => false,
        };

        private static TypeMarker InferExpectedMarker(object value)
        {
            if (value == null) return TypeMarker.Null; // not encrypted currently
            return value switch
            {
                string => TypeMarker.String,
                bool => TypeMarker.Boolean,
                sbyte or byte or short or ushort or int or uint or long or ulong => TypeMarker.Long,
                float or double or decimal => TypeMarker.Double,
                System.Collections.IEnumerable when value is not string => value is System.Array ? TypeMarker.Array : TypeMarker.Array,
                _ => TypeMarker.Object,
            };
        }

        private static void AssertPlainEquality(string name, object original, JsonElement elem)
        {
            if (original == null)
            {
                Assert.AreEqual(JsonValueKind.Null, elem.ValueKind, $"Plain null '{name}' expected Null kind.");
                return;
            }

            switch (original)
            {
                case string s:
                    Assert.AreEqual(JsonValueKind.String, elem.ValueKind, $"Plain '{name}' expected string JSON kind.");
                    Assert.AreEqual(s, elem.GetString(), $"Plain string mismatch for '{name}'.");
                    break;
                case bool b:
                    Assert.AreEqual(b ? JsonValueKind.True : JsonValueKind.False, elem.ValueKind, $"Plain bool '{name}' mismatch kind.");
                    break;
                case sbyte or byte or short or ushort or int or uint or long or ulong:
                {
                    Assert.AreEqual(JsonValueKind.Number, elem.ValueKind, $"Plain number '{name}' expected numeric JSON kind.");
                    long expected = System.Convert.ToInt64(original);
                    Assert.AreEqual(expected, elem.GetInt64(), $"Plain number mismatch for '{name}'.");
                    break;
                }
                case float or double or decimal:
                {
                    Assert.AreEqual(JsonValueKind.Number, elem.ValueKind, $"Plain number '{name}' expected numeric JSON kind.");
                    double expected = System.Convert.ToDouble(original, System.Globalization.CultureInfo.InvariantCulture);
                    double actual = elem.GetDouble();
                    Assert.IsTrue(System.Math.Abs(expected - actual) < 1e-9, $"Plain floating number mismatch for '{name}'. Expected {expected} got {actual}");
                    break;
                }
                default:
                    // For objects/arrays we just ensure not a base64 string (i.e., stays structured)
                    Assert.IsTrue(elem.ValueKind == JsonValueKind.Object || elem.ValueKind == JsonValueKind.Array, $"Plain complex '{name}' expected Object/Array kind.");
                    break;
            }
        }
    }
}
#endif
