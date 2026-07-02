//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Regression tests for JSON-fidelity and data-corruption defects in the Stream
    /// (System.Text.Json) encryption processor shipped (preview-only, opt-in) by PR #5478.
    /// Each test fails on the unfixed #5478 code and passes after the corresponding minimal fix.
    /// </summary>
    [TestClass]
    public class StreamProcessorDataCorruptionFixTests
    {
        private const string DekId = "dekId";
        private static Mock<Encryptor> mockEncryptor;

        private static readonly JsonSerializerOptions SystemTextOptions = new ()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ = ctx;

            // Small buffer size to exercise the leftover / buffer-growth and multi-segment
            // (HasValueSequence) reader paths against the pass-through/verbatim writers.
            PooledStreamConfiguration.SetConfiguration(new PooledStreamConfiguration { StreamProcessorBufferSize = 8 });

            mockEncryptor = TestEncryptorFactory.CreateMde(DekId, out _);
        }

        private static EncryptionOptions CreateOptions(IEnumerable<string> paths)
        {
            return new EncryptionOptions
            {
                DataEncryptionKeyId = DekId,
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = paths.ToList(),
            };
        }

        private static async Task<MemoryStream> EncryptObjAsync(object doc, EncryptionOptions options)
        {
            Stream input = TestCommon.ToStream(doc);
            return await EncryptStreamAsync(input, options);
        }

        private static async Task<MemoryStream> EncryptJsonAsync(string json, EncryptionOptions options)
        {
            using MemoryStream input = new (Encoding.UTF8.GetBytes(json));
            return await EncryptStreamAsync(input, options);
        }

        private static async Task<MemoryStream> EncryptStreamAsync(Stream input, EncryptionOptions options)
        {
            MemoryStream output = new ();
            await EncryptionProcessor.EncryptAsync(input, output, mockEncryptor.Object, options, JsonProcessor.Stream, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            return output;
        }

        private static EncryptionProperties ReadProperties(MemoryStream encrypted)
        {
            encrypted.Position = 0;
            using JsonDocument jd = JsonDocument.Parse(encrypted, new JsonDocumentOptions { AllowTrailingCommas = true });
            JsonElement ei = jd.RootElement.GetProperty(Constants.EncryptedInfo);
            EncryptionProperties props = JsonSerializer.Deserialize<EncryptionProperties>(ei.GetRawText(), SystemTextOptions);
            encrypted.Position = 0;
            return props;
        }

        private static async Task<JsonDocument> RoundTripAsync(MemoryStream encrypted)
        {
            EncryptionProperties props = ReadProperties(encrypted);
            MemoryStream output = new ();
            await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            return JsonDocument.Parse(output);
        }

        // Defect #1: a pass-through (non-encrypted) string value that contains JSON escape
        // sequences must not be escaped a second time during encrypt + decrypt.
        [TestMethod]
        public async Task RoundTrip_PassThroughStringWithJsonEscapes_PreservesValue()
        {
            string note = "he said \"hi\" \\ end\n\tline\u0001end";
            var doc = new { id = "1", enc = "secret", note };

            MemoryStream encrypted = await EncryptObjAsync(doc, CreateOptions(new[] { "/enc" }));
            using JsonDocument jd = await RoundTripAsync(encrypted);

            Assert.AreEqual(note, jd.RootElement.GetProperty("note").GetString());
        }

        // Defect #1: a pass-through property NAME that contains JSON escape sequences must not be
        // double-escaped during encrypt + decrypt.
        [TestMethod]
        public async Task RoundTrip_PassThroughPropertyNameWithJsonEscapes_PreservesName()
        {
            // Property name (semantic): we"ird\name
            string json = "{\"id\":\"1\",\"enc\":\"secret\",\"we\\\"ird\\\\name\":\"value\"}";

            MemoryStream encrypted = await EncryptJsonAsync(json, CreateOptions(new[] { "/enc" }));
            using JsonDocument jd = await RoundTripAsync(encrypted);

            bool found = false;
            foreach (JsonProperty p in jd.RootElement.EnumerateObject())
            {
                if (p.Name == "we\"ird\\name")
                {
                    found = true;
                    Assert.AreEqual("value", p.Value.GetString());
                }
            }

            Assert.IsTrue(found, "Pass-through property name with JSON escapes was not preserved verbatim.");
        }

        // Defect #1: string values written through the pass-through branch INSIDE an encrypted
        // object payload must not be double-escaped either.
        [TestMethod]
        public async Task RoundTrip_StringWithEscapesInsideEncryptedObject_PreservesValue()
        {
            string note = "va\\lue\nwith\"quote\u00e9";
            var doc = new { id = "1", secret = new { note } };

            MemoryStream encrypted = await EncryptObjAsync(doc, CreateOptions(new[] { "/secret" }));
            using JsonDocument jd = await RoundTripAsync(encrypted);

            JsonElement secret = jd.RootElement.GetProperty("secret");
            Assert.AreEqual(note, secret.GetProperty("note").GetString());
        }

        // Defect #2: a JSON null inside an encrypted object/array must not wipe the pending
        // encrypted path; _ep must record the real path and the payload must stay decryptable.
        [TestMethod]
        public async Task RoundTrip_NullInsideEncryptedObject_RemainsDecryptable()
        {
            var doc = new { id = "1", obj = new Dictionary<string, object> { ["a"] = null, ["b"] = "x" } };

            MemoryStream encrypted = await EncryptObjAsync(doc, CreateOptions(new[] { "/obj" }));

            // The encrypted _ep must contain the real path, never a null entry.
            EncryptionProperties props = ReadProperties(encrypted);
            CollectionAssert.AreEqual(new[] { "/obj" }, props.EncryptedPaths.ToList());

            using JsonDocument jd = await RoundTripAsync(encrypted);
            JsonElement obj = jd.RootElement.GetProperty("obj");
            Assert.AreEqual(JsonValueKind.Object, obj.ValueKind);
            Assert.AreEqual(JsonValueKind.Null, obj.GetProperty("a").ValueKind);
            Assert.AreEqual("x", obj.GetProperty("b").GetString());
        }

        // Defect #3: an integer literal outside Int64 range must be rejected (fail-closed) rather
        // than silently coerced to a lossy double (e.g. 1E+26).
        [TestMethod]
        public async Task Encrypt_OutOfRangeIntegerLiteral_Throws()
        {
            string json = "{\"id\":\"1\",\"n\":99999999999999999999999999}";
            EncryptionOptions options = CreateOptions(new[] { "/n" });

            try
            {
                await EncryptJsonAsync(json, options);
                Assert.Fail("Expected an exception for an out-of-range integer literal.");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains(ex.ToString(), "Int64");
            }
        }

        // Defect #4: a decrypted integral double (e.g. 5.0) must keep its double form (5.0)
        // instead of flipping to an integer (5).
        [TestMethod]
        public async Task RoundTrip_IntegralDouble_PreservesDotZero()
        {
            string json = "{\"id\":\"1\",\"d\":5.0}";

            MemoryStream encrypted = await EncryptJsonAsync(json, CreateOptions(new[] { "/d" }));
            using JsonDocument jd = await RoundTripAsync(encrypted);

            Assert.AreEqual("5.0", jd.RootElement.GetProperty("d").GetRawText());
        }

        // Defect #5 guard: a nested (non-top-level) property named _ei must NOT trigger the
        // top-level _ei guard. Encryption succeeds and the nested value round-trips (the decrypt
        // side _ei skip is already depth-gated in #5478), while the top-level metadata _ei is
        // still stripped.
        [TestMethod]
        public async Task Encrypt_NestedEiProperty_DoesNotThrow()
        {
            var doc = new { id = "1", enc = "x", payload = new Dictionary<string, object> { ["_ei"] = "userdata", ["keep"] = "v" } };

            MemoryStream encrypted = await EncryptObjAsync(doc, CreateOptions(new[] { "/enc" }));
            using JsonDocument jd = await RoundTripAsync(encrypted);

            Assert.AreEqual("v", jd.RootElement.GetProperty("payload").GetProperty("keep").GetString());
            Assert.AreEqual("userdata", jd.RootElement.GetProperty("payload").GetProperty("_ei").GetString());
            Assert.IsFalse(jd.RootElement.TryGetProperty(Constants.EncryptedInfo, out _), "Top-level _ei metadata must be stripped on decrypt.");
        }

        // Defect #7: the Stream _ei deserializer must tolerate a quoted _ef ("3") like the
        // Newtonsoft processor does, reading it as EncryptionFormatVersion 3 without throwing.
        [TestMethod]
        public async Task ReadEncryptionProperties_EfAsQuotedString_ParsesAsMdeVersion()
        {
            string json = "{\"id\":\"a\",\"_ei\":{\"_ef\":\"3\",\"_ea\":\"AEAD_AES_256_CBC_HMAC_SHA256_RANDOMIZED\",\"_en\":\"dekId\",\"_ep\":[\"/p\"]}}";
            await using MemoryStream stream = new (Encoding.UTF8.GetBytes(json));

            EncryptionProperties result = await EncryptionPropertiesStreamReader.ReadAsync(stream, PooledJsonSerializer.SerializerOptions, CancellationToken.None);

            Assert.IsNotNull(result);
            Assert.AreEqual(EncryptionFormatVersion.Mde, result.EncryptionFormatVersion);
        }

        // Defect #8: the (Stream, DecryptionContext) decrypt overload must dispose the input stream
        // on a successful MDE decrypt, matching the Newtonsoft adapter's stream-ownership contract.
        [TestMethod]
        public async Task DecryptAsync_StreamOverload_DisposesInputOnSuccess()
        {
            MemoryStream encrypted = await EncryptObjAsync(new { id = "1", s = "secret" }, CreateOptions(new[] { "/s" }));

            SystemTextJsonStreamAdapter adapter = new (new StreamProcessor());
            (Stream output, DecryptionContext context) = await adapter.DecryptAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);

            Assert.IsNotNull(context);
            Assert.IsFalse(encrypted.CanRead, "Input stream should be disposed after a successful decrypt.");
            await output.DisposeAsync();
        }
    }
}
#endif
