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
        public static void AssertEncryptedRawJson(
            string rawJson,
            IEnumerable<string> plaintextValuesEncrypted,
            IEnumerable<string> expectedPlainValues = null,
            IEnumerable<string> encryptedBooleanPropertyNames = null)
        {
            Assert.IsFalse(string.IsNullOrEmpty(rawJson), "rawJson was null/empty");
            Assert.IsTrue(rawJson.Contains("\"_ei\""), "_ei metadata missing (encryption properties)");

            if (plaintextValuesEncrypted != null)
            {
                foreach (string val in plaintextValuesEncrypted.Where(v => !string.IsNullOrEmpty(v)))
                {
                    string quoted = "\"" + val + "\""; // expected form if accidentally left in clear text
                    Assert.IsFalse(rawJson.Contains(quoted), $"Found plaintext value '{val}' in stored JSON; expected it to be encrypted.");
                }
            }

            if (expectedPlainValues != null)
            {
                foreach (string plain in expectedPlainValues.Where(v => !string.IsNullOrEmpty(v)))
                {
                    Assert.IsTrue(rawJson.Contains(plain), $"Expected plain value '{plain}' not found in JSON.");
                }
            }

            if (encryptedBooleanPropertyNames != null)
            {
                foreach (string name in encryptedBooleanPropertyNames.Where(v => !string.IsNullOrEmpty(v)))
                {
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
        }
    }
}
#endif
