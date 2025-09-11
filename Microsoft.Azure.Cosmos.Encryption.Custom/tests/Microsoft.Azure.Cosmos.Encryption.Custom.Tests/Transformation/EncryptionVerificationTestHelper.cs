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
        /// <param name="rawJson">Full JSON string produced by encryption.</param>
        /// <param name="stringPlaintextValuesEncrypted">Plaintext STRING values that SHOULD have been encrypted.</param>
        /// <param name="numericPlaintextValuesEncrypted">Plaintext NUMERIC values (in invariant string form) that SHOULD have been encrypted.</param>
        /// <param name="expectedPlainValues">Values expected to remain in clear-text (matched with simple Contains).</param>
        public static void AssertEncryptedRawJson(
            string rawJson,
            IEnumerable<string> stringPlaintextValuesEncrypted,
            IEnumerable<string> numericPlaintextValuesEncrypted = null,
            IEnumerable<string> expectedPlainValues = null,
            IEnumerable<string> encryptedBooleanPropertyNames = null)
        {
            Assert.IsFalse(string.IsNullOrEmpty(rawJson), "rawJson was null/empty");
            Assert.IsTrue(rawJson.Contains("\"_ei\""), "_ei metadata missing (encryption properties)");

            if (stringPlaintextValuesEncrypted != null)
            {
                foreach (string val in stringPlaintextValuesEncrypted.Where(v => !string.IsNullOrEmpty(v)))
                {
                    string quoted = "\"" + val + "\""; // How it would appear if unintentionally left in plaintext (as a JSON string)
                    Assert.IsFalse(rawJson.Contains(quoted), $"Found plaintext value '{val}' in stored JSON; expected it to be encrypted.");
                }
            }

            if (numericPlaintextValuesEncrypted != null)
            {
                foreach (string num in numericPlaintextValuesEncrypted.Where(v => !string.IsNullOrEmpty(v)))
                {
                    // Very simple heuristic: look for :<num>[,}] ignoring whitespace.
                    // Build patterns without whitespace to reduce complexity.
                    string p1 = ":" + num + ",";
                    string p2 = ":" + num + "}";
                    // Whitespace variants (common minimal) - a single space after ':'
                    string p3 = ": " + num + ",";
                    string p4 = ": " + num + "}";
                    bool found = rawJson.Contains(p1) || rawJson.Contains(p2) || rawJson.Contains(p3) || rawJson.Contains(p4);
                    Assert.IsFalse(found, $"Found numeric plaintext value '{num}' in stored JSON; expected it to be encrypted.");
                }
            }

            if (encryptedBooleanPropertyNames != null)
            {
                foreach (string name in encryptedBooleanPropertyNames.Where(v => !string.IsNullOrEmpty(v)))
                {
                    // Look for explicit property boolean tokens: "Name":true / "Name": false / with comma or end-object following.
                    // If any appear, boolean wasn't encrypted.
                    string[] patterns = new[]
                    {
                        "\"" + name + "\":true",
                        "\"" + name + "\": true",
                        "\"" + name + "\":false",
                        "\"" + name + "\": false",
                    };
                    bool found = patterns.Any(p => rawJson.Contains(p));
                    Assert.IsFalse(found, $"Found plaintext boolean for encrypted property '{name}' in JSON.");
                }
            }

            if (expectedPlainValues != null)
            {
                foreach (string plain in expectedPlainValues.Where(v => !string.IsNullOrEmpty(v)))
                {
                    Assert.IsTrue(rawJson.Contains(plain), $"Expected plain value '{plain}' not found in JSON.");
                }
            }
        }
    }
}
#endif
