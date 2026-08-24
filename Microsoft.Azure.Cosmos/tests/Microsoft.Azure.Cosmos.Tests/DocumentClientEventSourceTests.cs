//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Reflection;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DocumentClientEventSourceTests
    {
        private const string SecretAuthHeaderValue =
            "type=master&ver=1.0&sig=DOCDBAUTHSECRETSIGNATUREabcdef0123456789";

        /// <summary>
        /// Regression test for the credential-leak issue where the
        /// "DocumentDBClient" EventSource (Event ID 1 - Request) wrote the
        /// raw Authorization HTTP header into the ETW event payload. Any
        /// subscriber at Verbose level (for example a Geneva / GCS EtwProvider
        /// named "DocumentDBClient") would have captured the master-key HMAC,
        /// resource token, or AAD Bearer token in plaintext.
        ///
        /// This test exercises the redaction helper directly (no ETW listener
        /// wiring) so it is deterministic under parallel test execution. The
        /// [NonEvent] public Request wrapper calls this helper on the
        /// headerValues array immediately before forwarding it to the
        /// ETW-emitting [Event(1)] method, so covering it here covers the leak
        /// fix end-to-end.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public void RedactSensitiveHeaderValues_ReplacesAuthorizationWithRedacted()
        {
            int authorizationIndex = GetAuthorizationIndex();
            string[] headerValues = BuildHeaderValuesArray();
            headerValues[authorizationIndex] = SecretAuthHeaderValue;

            DocumentClientEventSource.RedactSensitiveHeaderValues(headerValues);

            Assert.AreEqual(
                "REDACTED",
                headerValues[authorizationIndex],
                "Authorization header must be redacted before being forwarded to the ETW emit path.");

            // Defense in depth: no slot anywhere in the array should leak the secret value.
            for (int i = 0; i < headerValues.Length; i++)
            {
                string slot = headerValues[i];
                if (slot == null)
                {
                    continue;
                }

                Assert.IsFalse(
                    slot.IndexOf("DOCDBAUTHSECRETSIGNATURE", StringComparison.Ordinal) >= 0,
                    $"Header slot [{i}] contained the secret authorization signature: '{slot}'.");
            }
        }

        /// <summary>
        /// When no Authorization header is present (for example internal
        /// plumbing requests) the redaction logic must not change behaviour:
        /// the pre-existing empty-string / null slot should be left alone, not
        /// replaced with the literal "REDACTED".
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public void RedactSensitiveHeaderValues_LeavesEmptyAuthorizationUntouched()
        {
            int authorizationIndex = GetAuthorizationIndex();

            string[] emptyValues = BuildHeaderValuesArray();
            emptyValues[authorizationIndex] = string.Empty;
            DocumentClientEventSource.RedactSensitiveHeaderValues(emptyValues);
            Assert.AreEqual(string.Empty, emptyValues[authorizationIndex]);

            string[] nullValues = BuildHeaderValuesArray();
            nullValues[authorizationIndex] = null;
            DocumentClientEventSource.RedactSensitiveHeaderValues(nullValues);
            Assert.IsNull(nullValues[authorizationIndex]);
        }

        /// <summary>
        /// Guards against future refactors that might pass a null array or a
        /// shorter array into the redaction helper.
        /// </summary>
        [TestMethod]
        [Owner("ntripician")]
        public void RedactSensitiveHeaderValues_HandlesDegenerateInputsSafely()
        {
            DocumentClientEventSource.RedactSensitiveHeaderValues(null);
            DocumentClientEventSource.RedactSensitiveHeaderValues(Array.Empty<string>());
            DocumentClientEventSource.RedactSensitiveHeaderValues(new string[] { string.Empty });
        }

        private static string[] GetRequestHeaderKeysToExtract()
        {
            FieldInfo field = typeof(DocumentClientEventSource).GetField(
                "RequestHeaderKeysToExtract",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "RequestHeaderKeysToExtract field must be present on DocumentClientEventSource.");
            return (string[])field.GetValue(null);
        }

        private static int GetAuthorizationIndex()
        {
            int index = Array.IndexOf(GetRequestHeaderKeysToExtract(), HttpConstants.HttpHeaders.Authorization);
            Assert.IsTrue(index >= 0, "Authorization must be present in RequestHeaderKeysToExtract.");
            return index;
        }

        private static string[] BuildHeaderValuesArray()
        {
            string[] keys = GetRequestHeaderKeysToExtract();
            string[] values = new string[keys.Length];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = string.Empty;
            }

            return values;
        }
    }
}
