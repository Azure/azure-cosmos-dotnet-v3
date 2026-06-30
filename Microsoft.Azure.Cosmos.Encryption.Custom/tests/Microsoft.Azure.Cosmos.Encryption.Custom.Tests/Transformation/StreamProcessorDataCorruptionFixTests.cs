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

        // Encrypts via the Newtonsoft processor (the canonical top-level-only reference) and returns a
        // seekable copy of the wire bytes for cross-processor parity assertions.
        private static async Task<MemoryStream> EncryptNewtonsoftAsync(object doc, EncryptionOptions options)
        {
            Stream input = TestCommon.ToStream(doc);
            Stream encrypted = await EncryptionProcessor.EncryptAsync(input, mockEncryptor.Object, options, JsonProcessor.Newtonsoft, new CosmosDiagnosticsContext(), CancellationToken.None);
            MemoryStream copy = new();
            encrypted.Position = 0;
            await encrypted.CopyToAsync(copy);
            copy.Position = 0;
            return copy;
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
        // The decrypt-side _ei skip is now depth-gated too, so a nested user property literally named
        // _ei is preserved verbatim — only the top-level _ei metadata block is stripped (matching the
        // Newtonsoft processor, which removes only the root _ei).
        [TestMethod]
        public async Task Encrypt_NestedEiProperty_DoesNotThrow()
        {
            var doc = new { id = "1", enc = "x", payload = new Dictionary<string, object> { ["_ei"] = "userdata", ["keep"] = "v" } };

            MemoryStream encrypted = await EncryptObjAsync(doc, CreateOptions(new[] { "/enc" }));
            using JsonDocument jd = await RoundTripAsync(encrypted);

            Assert.AreEqual("x", jd.RootElement.GetProperty("enc").GetString());
            Assert.AreEqual("v", jd.RootElement.GetProperty("payload").GetProperty("keep").GetString());

            // The nested user property named _ei (depth 2) must survive decrypt; only the top-level
            // metadata _ei (depth 1) is removed. Before the depth==1 guard the decryptor skipped any
            // property named _ei at any depth, silently dropping this nested value.
            Assert.AreEqual("userdata", jd.RootElement.GetProperty("payload").GetProperty("_ei").GetString());
            Assert.IsFalse(jd.RootElement.TryGetProperty(Constants.EncryptedInfo, out _), "Top-level _ei metadata must be stripped on decrypt.");
        }

        // BLOCKING data-correctness regression: the Stream encryptor matched configured encrypted-path
        // property names at ANY depth, diverging from the Newtonsoft processor (top-level only). A
        // nested property sharing a top-level encrypted-path name under a NON-encrypted parent must stay
        // plaintext, only the top-level value is encrypted, and the result must match Newtonsoft.
        [TestMethod]
        public async Task Encrypt_NestedPropertySharingTopLevelEncryptedPathName_OnlyTopLevelEncrypted_MatchesNewtonsoft()
        {
            var doc = new { id = "1", Sensitive = "topsecret", Outer = new { Sensitive = "nestedplain" } };
            EncryptionOptions options = CreateOptions(new[] { "/Sensitive" });

            MemoryStream streamEnc = await EncryptObjAsync(doc, options);
            MemoryStream nsEnc = await EncryptNewtonsoftAsync(doc, options);

            // Only the top-level path is recorded as encrypted; the nested match must not add a duplicate.
            CollectionAssert.AreEqual(new[] { "/Sensitive" }, ReadProperties(streamEnc).EncryptedPaths.ToList());
            CollectionAssert.AreEqual(new[] { "/Sensitive" }, ReadProperties(nsEnc).EncryptedPaths.ToList());

            streamEnc.Position = 0;
            nsEnc.Position = 0;
            using JsonDocument streamJd = JsonDocument.Parse(streamEnc, new JsonDocumentOptions { AllowTrailingCommas = true });
            using JsonDocument nsJd = JsonDocument.Parse(nsEnc, new JsonDocumentOptions { AllowTrailingCommas = true });

            // Nested Outer.Sensitive stays plaintext under both processors (the bug encrypted it in Stream).
            Assert.AreEqual("nestedplain", streamJd.RootElement.GetProperty("Outer").GetProperty("Sensitive").GetString());
            Assert.AreEqual("nestedplain", nsJd.RootElement.GetProperty("Outer").GetProperty("Sensitive").GetString());

            // Cross-processor parity: deterministic mock cipher => top-level encrypted value is byte-identical,
            // and the untouched Outer subtree is structurally identical.
            Assert.AreEqual(
                nsJd.RootElement.GetProperty("Sensitive").GetString(),
                streamJd.RootElement.GetProperty("Sensitive").GetString(),
                "Top-level encrypted value must match the Newtonsoft processor output.");
            Assert.AreEqual(
                nsJd.RootElement.GetProperty("Outer").GetRawText(),
                streamJd.RootElement.GetProperty("Outer").GetRawText());

            // Full round-trip through the Stream decryptor recovers the original document exactly.
            using JsonDocument round = await RoundTripAsync(streamEnc);
            Assert.AreEqual("topsecret", round.RootElement.GetProperty("Sensitive").GetString());
            Assert.AreEqual("nestedplain", round.RootElement.GetProperty("Outer").GetProperty("Sensitive").GetString());
        }

        // BLOCKING data-correctness regression (decrypt side): a document produced by the Newtonsoft
        // processor (top-level encrypted only, nested same-named property left as plaintext) must decrypt
        // correctly through the Stream processor. Before the depth==1 guard the Stream decryptor matched
        // the nested property name and tried to Base64-decode the nested plaintext, throwing/corrupting it.
        [TestMethod]
        public async Task Decrypt_NestedPropertySharingEncryptedPathName_FromNewtonsoftWire_StaysPlaintext()
        {
            var doc = new { id = "1", Sensitive = "topsecret", Outer = new { Sensitive = "nestedplain" } };
            EncryptionOptions options = CreateOptions(new[] { "/Sensitive" });

            MemoryStream nsEnc = await EncryptNewtonsoftAsync(doc, options);

            using JsonDocument jd = await RoundTripAsync(nsEnc);

            Assert.AreEqual("topsecret", jd.RootElement.GetProperty("Sensitive").GetString());
            Assert.AreEqual("nestedplain", jd.RootElement.GetProperty("Outer").GetProperty("Sensitive").GetString());
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
