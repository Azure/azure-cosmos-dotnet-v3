//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if ENCRYPTION_CUSTOM_PREVIEW && NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.EmulatorTests
{
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Shared strong verification helper for emulator tests (raw storage validation) and parity with unit tests.
    /// </summary>
    public static class EncryptionVerificationTestHelper
    {
        // Mirror of unit-test helper unified verification.
        public static void AssertEncryptedDocument(
            string rawJson,
            IReadOnlyDictionary<string, object> encryptedProperties,
            IReadOnlyDictionary<string, object> plainProperties = null)
        {
            Assert.IsFalse(string.IsNullOrEmpty(rawJson), "rawJson was null/empty");
            using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            Assert.IsTrue(root.TryGetProperty("_ei", out _), "_ei metadata missing (encryption properties)");

            foreach (var kvp in encryptedProperties)
            {
                string name = kvp.Key;
                object original = kvp.Value;
                Assert.IsTrue(root.TryGetProperty(name, out var encElem), $"Encrypted property '{name}' missing");

                if (original == null)
                {
                    Assert.AreEqual(System.Text.Json.JsonValueKind.Null, encElem.ValueKind, $"Null encrypted path '{name}' expected null kind.");
                    continue;
                }

                Assert.AreEqual(System.Text.Json.JsonValueKind.String, encElem.ValueKind, $"Encrypted property '{name}' should be base64 string.");
                string b64 = encElem.GetString();
                Assert.IsFalse(string.IsNullOrEmpty(b64), $"Encrypted property '{name}' empty.");

                string originalSerialized = SerializeOriginalPrimitive(original);
                Assert.AreNotEqual(originalSerialized, b64, $"Property '{name}' appears plaintext.");

                byte[] decoded;
                try { decoded = System.Convert.FromBase64String(b64); }
                catch { Assert.Fail($"Encrypted property '{name}' invalid base64."); return; }
                Assert.IsTrue(decoded.Length >= 1, $"Ciphertext '{name}' too short.");
                var marker = (TypeMarker)decoded[0];
                Assert.IsTrue(IsValidMarker(marker), $"Invalid type marker {(int)decoded[0]} for '{name}'.");

                var expected = InferExpectedMarker(original);
                if (IsPrimitiveMarker(expected))
                {
                    Assert.AreEqual(expected, marker, $"Type marker mismatch for '{name}'. Expected {expected} got {marker}.");
                }
            }

            if (plainProperties != null)
            {
                foreach (var kvp in plainProperties)
                {
                    string name = kvp.Key;
                    object original = kvp.Value;
                    Assert.IsTrue(root.TryGetProperty(name, out var elem), $"Plain property '{name}' missing");
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
                System.IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
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
            if (value == null) return TypeMarker.Null;
            return value switch
            {
                string => TypeMarker.String,
                bool => TypeMarker.Boolean,
                sbyte or byte or short or ushort or int or uint or long or ulong => TypeMarker.Long,
                float or double or decimal => TypeMarker.Double,
                System.Collections.IEnumerable when value is not string => TypeMarker.Array,
                _ => TypeMarker.Object,
            };
        }
        private static void AssertPlainEquality(string name, object original, System.Text.Json.JsonElement elem)
        {
            if (original == null)
            {
                Assert.AreEqual(System.Text.Json.JsonValueKind.Null, elem.ValueKind, $"Plain null '{name}' expected Null kind.");
                return;
            }
            switch (original)
            {
                case string s:
                    Assert.AreEqual(System.Text.Json.JsonValueKind.String, elem.ValueKind, $"Plain '{name}' expected string kind.");
                    Assert.AreEqual(s, elem.GetString(), $"Plain string mismatch '{name}'.");
                    break;
                case bool b:
                    Assert.AreEqual(b ? System.Text.Json.JsonValueKind.True : System.Text.Json.JsonValueKind.False, elem.ValueKind, $"Plain bool '{name}' mismatch.");
                    break;
                case sbyte or byte or short or ushort or int or uint or long or ulong:
                    Assert.AreEqual(System.Text.Json.JsonValueKind.Number, elem.ValueKind, $"Plain number '{name}' expected numeric kind.");
                    long expected = System.Convert.ToInt64(original);
                    Assert.AreEqual(expected, elem.GetInt64(), $"Plain number mismatch '{name}'.");
                    break;
                case float or double or decimal:
                    Assert.AreEqual(System.Text.Json.JsonValueKind.Number, elem.ValueKind, $"Plain number '{name}' expected numeric kind.");
                    double expectedD = System.Convert.ToDouble(original, System.Globalization.CultureInfo.InvariantCulture);
                    double actual = elem.GetDouble();
                    Assert.IsTrue(System.Math.Abs(expectedD - actual) < 1e-9, $"Plain floating mismatch '{name}' expected {expectedD} got {actual}");
                    break;
                default:
                    Assert.IsTrue(elem.ValueKind == System.Text.Json.JsonValueKind.Object || elem.ValueKind == System.Text.Json.JsonValueKind.Array, $"Plain complex '{name}' expected Object/Array kind.");
                    break;
            }
        }
    }
}
#endif
