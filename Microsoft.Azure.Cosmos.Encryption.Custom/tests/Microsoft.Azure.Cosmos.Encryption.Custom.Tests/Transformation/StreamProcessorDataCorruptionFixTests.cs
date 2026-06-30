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
    /// Regression tests for the Stream (System.Text.Json) processor data-corruption and fidelity
    /// defects absorbed from the triage of PRs #5959 and #5903. Each test fails on the pre-fix
    /// base and passes after the minimal fix.
    /// </summary>
    [TestClass]
    public class StreamProcessorDataCorruptionFixTests
    {
        private const string DekId = "dekId";
        private static Mock<Encryptor> mockEncryptor;

        private static readonly JsonSerializerOptions SystemTextOptions = new()
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
        };

        [ClassInitialize]
        public static void Init(TestContext ctx)
        {
            _ = ctx;

            // Small initial buffer size to exercise leftover / buffer-growth paths.
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
            using MemoryStream input = new(Encoding.UTF8.GetBytes(json));
            return await EncryptStreamAsync(input, options);
        }

        private static async Task<MemoryStream> EncryptStreamAsync(Stream input, EncryptionOptions options)
        {
            MemoryStream output = new();
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
            MemoryStream output = new();
            await new StreamProcessor().DecryptStreamAsync(encrypted, output, mockEncryptor.Object, props, new CosmosDiagnosticsContext(), CancellationToken.None);
            output.Position = 0;
            return JsonDocument.Parse(output);
        }

        // Defect #1: pass-through string value containing JSON escape sequences must not be
        // double-escaped during encrypt + decrypt.
        [TestMethod]
        public async Task RoundTrip_PassThroughStringWithJsonEscapes_PreservesValue()
        {
            string note = "he said \"hi\" \\ end\n\tline\u0001end";
            var doc = new { id = "1", enc = "secret", note };

            MemoryStream encrypted = await EncryptObjAsync(doc, CreateOptions(new[] { "/enc" }));
            using JsonDocument jd = await RoundTripAsync(encrypted);

            Assert.AreEqual(note, jd.RootElement.GetProperty("note").GetString());
        }

        // Defect #1: pass-through property name containing JSON escape sequences must not be
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

        // Defect #1: string values inside an encrypted object payload (written through the
        // pass-through branch) must not be double-escaped.
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
        // encrypted path; the payload must remain decryptable and _ep must record the real path.
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
        // than silently coerced to a lossy double.
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

        // Defect #4: a decrypted integral double (e.g. 5.0) must keep its double type marker form
        // (written as 5.0) instead of flipping to 5.
        [TestMethod]
        public async Task RoundTrip_IntegralDouble_PreservesDotZero()
        {
            string json = "{\"id\":\"1\",\"d\":5.0}";

            MemoryStream encrypted = await EncryptJsonAsync(json, CreateOptions(new[] { "/d" }));
            using JsonDocument jd = await RoundTripAsync(encrypted);

            Assert.AreEqual("5.0", jd.RootElement.GetProperty("d").GetRawText());
        }

        // Defect #5: encrypting a document that already carries a top-level _ei must fail with a
        // clear error instead of silently emitting a duplicate _ei property.
        [TestMethod]
        public async Task Encrypt_DocumentWithTopLevelEi_Throws()
        {
            string json = "{\"_ei\":\"already\",\"id\":\"1\",\"enc\":\"x\"}";
            EncryptionOptions options = CreateOptions(new[] { "/enc" });

            try
            {
                await EncryptJsonAsync(json, options);
                Assert.Fail("Expected an exception when encrypting a document that already has a top-level _ei property.");
            }
            catch (InvalidOperationException ex)
            {
                StringAssert.Contains(ex.ToString(), Constants.EncryptedInfo);
            }
        }

        // Defect #5 negative case: a nested (non-top-level) property named _ei must NOT trigger the
        // top-level _ei guard. Encryption must succeed and the surrounding payload must round-trip.
        // (Note: the decryptor independently drops any property literally named _ei, which is a
        // separate pre-existing behavior and out of scope here, so we do not assert on payload._ei.)
        [TestMethod]
        public async Task Encrypt_NestedEiProperty_DoesNotThrow()
        {
            var doc = new { id = "1", enc = "x", payload = new Dictionary<string, object> { ["_ei"] = "userdata", ["keep"] = "v" } };

            MemoryStream encrypted = await EncryptObjAsync(doc, CreateOptions(new[] { "/enc" }));
            using JsonDocument jd = await RoundTripAsync(encrypted);

            Assert.AreEqual("x", jd.RootElement.GetProperty("enc").GetString());
            Assert.AreEqual("v", jd.RootElement.GetProperty("payload").GetProperty("keep").GetString());
        }

        // Defect #8: the (Stream, DecryptionContext) decrypt overload must dispose the input stream
        // on a successful MDE decrypt, matching the Newtonsoft adapter's stream-ownership contract.
        [TestMethod]
        public async Task DecryptAsync_StreamOverload_DisposesInputOnSuccess()
        {
            MemoryStream encrypted = await EncryptObjAsync(new { id = "1", s = "secret" }, CreateOptions(new[] { "/s" }));

            SystemTextJsonStreamAdapter adapter = new(new StreamProcessor());
            (Stream output, DecryptionContext context) = await adapter.DecryptAsync(encrypted, mockEncryptor.Object, new CosmosDiagnosticsContext(), CancellationToken.None);

            Assert.IsNotNull(context);
            Assert.IsFalse(encrypted.CanRead, "Input stream should be disposed after a successful decrypt.");
            await output.DisposeAsync();
        }
    }
}
#endif
