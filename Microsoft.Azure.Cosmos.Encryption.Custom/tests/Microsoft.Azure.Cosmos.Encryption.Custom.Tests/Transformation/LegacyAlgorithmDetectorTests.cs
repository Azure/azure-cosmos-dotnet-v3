//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Tests.Transformation
{
    using System.Text;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Custom.Transformation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CS0618 // Type or member is obsolete (legacy algorithm name)
    [TestClass]
    public class LegacyAlgorithmDetectorTests
    {
        private const string LegacyAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized;
        private const string MdeAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;

        private static LegacyAlgorithmDetector.DetectionResult Detect(string json)
        {
            return LegacyAlgorithmDetector.Detect(Encoding.UTF8.GetBytes(json));
        }

        [TestMethod]
        public void Detect_Empty_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, LegacyAlgorithmDetector.Detect(System.ReadOnlySpan<byte>.Empty));
        }

        [TestMethod]
        public void Detect_WhitespaceOnly_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("   \t\r\n  "));
        }

        [TestMethod]
        public void Detect_TopLevelArray_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("[1, 2, 3]"));
        }

        [TestMethod]
        public void Detect_TopLevelString_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("\"hello\""));
        }

        [TestMethod]
        public void Detect_TopLevelNumber_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("42"));
        }

        [TestMethod]
        public void Detect_EmptyObject_ReturnsNotEncrypted()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.NotEncrypted, Detect("{}"));
        }

        [TestMethod]
        public void Detect_PlainObjectWithoutEi_ReturnsNotEncrypted()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.NotEncrypted, Detect("{\"id\":\"42\",\"pk\":\"p1\",\"NonSensitive\":\"v\"}"));
        }

        [TestMethod]
        public void Detect_NestedObjectsAndArrays_NoEi_ReturnsNotEncrypted()
        {
            string json = "{\"id\":\"42\",\"obj\":{\"a\":1,\"b\":[1,2,{\"c\":\"x\"}]},\"arr\":[\"x\",\"y\"],\"nullField\":null}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.NotEncrypted, Detect(json));
        }

        [TestMethod]
        public void Detect_EiIsNotAnObject_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":\"notAnObject\"}"));
        }

        [TestMethod]
        public void Detect_EiIsNull_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":null}"));
        }

        [TestMethod]
        public void Detect_EiIsArray_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":[1,2,3]}"));
        }

        [TestMethod]
        public void Detect_EiWithLegacyAlgorithm_ReturnsLegacyAlgorithm()
        {
            string json = $"{{\"id\":\"42\",\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\",\"_en\":\"dek1\",\"_ed\":\"AAEC\",\"_ef\":2,\"_ep\":[\"/x\"]}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_EiWithMdeAlgorithm_ReturnsMdeAlgorithm()
        {
            string json = $"{{\"id\":\"42\",\"_ei\":{{\"_ea\":\"{MdeAlgorithm}\",\"_en\":\"dek1\",\"_ed\":\"AAEC\",\"_ef\":3,\"_ep\":[\"/x\"]}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.MdeAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_EiWithUnknownAlgorithmString_ReturnsUnknown()
        {
            // Contract: only the exact MDE algorithm string is routed straight to MdeEncryptionProcessor.
            // Future/unknown identifiers fall through to the JObject path so behaviour stays byte-for-byte
            // identical to non-opt-in callers (the legacy Newtonsoft path tolerates these shapes).
            string json = "{\"_ei\":{\"_ea\":\"SomeFutureAlgo-v9\",\"_ed\":\"AA\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_EiWithEmptyAlgorithmString_ReturnsUnknown()
        {
            // Empty algorithm string is neither legacy nor MDE; route through JObject path for safe fallback.
            string json = "{\"_ei\":{\"_ea\":\"\",\"_ed\":\"AA\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_EiWithoutEaProperty_ReturnsUnknown()
        {
            string json = "{\"_ei\":{\"_en\":\"dek1\",\"_ed\":\"AA\",\"_ef\":2,\"_ep\":[\"/x\"]}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_EiEmpty_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":{}}"));
        }

        [TestMethod]
        public void Detect_EaIsInteger_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":{\"_ea\":42}}"));
        }

        [TestMethod]
        public void Detect_EaIsNull_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":{\"_ea\":null}}"));
        }

        [TestMethod]
        public void Detect_EaIsArray_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":{\"_ea\":[\"a\"]}}"));
        }

        [TestMethod]
        public void Detect_EaIsObject_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":{\"_ea\":{\"x\":1}}}"));
        }

        [TestMethod]
        public void Detect_EaIsBoolean_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":{\"_ea\":true}}"));
        }

        [TestMethod]
        public void Detect_EiIsFirstProperty_LegacyDetected()
        {
            string json = $"{{\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\"}},\"id\":\"42\",\"pk\":\"p1\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_EiIsLastProperty_LegacyDetected()
        {
            string json = $"{{\"id\":\"42\",\"pk\":\"p1\",\"NonSensitive\":\"v\",\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_EiInMiddle_LegacyDetected()
        {
            string json = $"{{\"id\":\"42\",\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\"}},\"trailing\":\"x\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_EaIsFirstChildOfEi_LegacyDetected()
        {
            string json = $"{{\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\",\"_en\":\"dek\",\"_ed\":\"AA\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_EaIsLastChildOfEi_LegacyDetected()
        {
            string json = $"{{\"_ei\":{{\"_en\":\"dek\",\"_ed\":\"AA\",\"_ef\":2,\"_ep\":[\"/x\"],\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_EaInMiddleOfEiChildren_LegacyDetected()
        {
            string json = $"{{\"_ei\":{{\"_en\":\"dek\",\"_ea\":\"{LegacyAlgorithm}\",\"_ed\":\"AA\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_PrecedingPropertiesAreObjectsAndArrays_LegacyDetected()
        {
            // Detector must correctly skip nested objects/arrays in other top-level properties.
            string json = $"{{\"obj\":{{\"_ei\":{{\"_ea\":\"{MdeAlgorithm}\"}}}},\"arr\":[{{\"_ei\":1}},2],\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_NestedEiInOtherProperty_NoTopLevelEi_ReturnsNotEncrypted()
        {
            // _ei nested inside another property must not match — only top-level _ei.
            string json = "{\"wrapper\":{\"_ei\":{\"_ea\":\"AEAes256CbcHmacSha256Randomized\"}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.NotEncrypted, Detect(json));
        }

        [TestMethod]
        public void Detect_EiChildContainsNestedObject_LegacyDetected()
        {
            // _ei has nested objects/arrays as sibling values of _ea — detector must skip them.
            string json = $"{{\"_ei\":{{\"_ed\":\"AAA\",\"nested\":{{\"foo\":[1,2,3]}},\"_ea\":\"{LegacyAlgorithm}\",\"_ep\":[\"/x\",\"/y\"]}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_PrettyPrintedJson_LegacyDetected()
        {
            string json = "{\n  \"id\": \"42\",\n  \"_ei\": {\n    \"_ea\": \"" + LegacyAlgorithm + "\",\n    \"_ed\": \"AAA\"\n  }\n}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_MalformedJson_AlgorithmFoundBeforeTruncation_StillReturnsLegacy()
        {
            // Once _ea is matched the detector returns immediately — any malformation past that
            // point is irrelevant to detector classification. This documents the short-circuit
            // contract; downstream MDE/JObject parsing surfaces the malformation appropriately.
            string json = $"{{\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\"";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_MalformedJson_TruncatedBeforeEa_ReturnsUnknown()
        {
            // Truncation occurs *before* _ea is read — detector cannot classify and returns Unknown.
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":{\"_en\":\"dek1\",\"_ed\":\"AA"));
        }

        [TestMethod]
        public void Detect_MalformedJson_GarbageBytes_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{ not valid json"));
        }

        [TestMethod]
        public void Detect_MalformedJson_TruncatedAfterEa_ReturnsUnknown()
        {
            // _ea property name appears but the value is missing — Utf8JsonReader should throw, we return Unknown.
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("{\"_ei\":{\"_ea\""));
        }

        [TestMethod]
        public void Detect_AlgorithmStringPrefixOnly_NotMatched_ReturnsUnknown()
        {
            // "AEAes256CbcHmacSha256" is a prefix of the legacy name but is not the exact value — and not the MDE value either — so the detector returns Unknown for safe JObject fallback.
            string json = "{\"_ei\":{\"_ea\":\"AEAes256CbcHmacSha256\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_AlgorithmStringWithExtraSuffix_NotMatched_ReturnsUnknown()
        {
            string json = $"{{\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}_v2\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_AlgorithmStringCaseMismatch_NotMatched_ReturnsUnknown()
        {
            // ValueTextEquals is ordinal/case-sensitive. The on-wire value is well-known and case-sensitive,
            // so any case-deviation matches neither the legacy nor the MDE algorithm and must be classified as Unknown.
            string json = "{\"_ei\":{\"_ea\":\"aeaes256cbchmacsha256randomized\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_MdeAlgorithmCaseMismatch_NotMatched_ReturnsUnknown()
        {
            // MDE name matching is also case-sensitive — case variants must fall through to JObject path.
            string json = "{\"_ei\":{\"_ea\":\"mdeaeadaes256cbchmac256randomized\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_MdeAlgorithmWithExtraSuffix_NotMatched_ReturnsUnknown()
        {
            string json = $"{{\"_ei\":{{\"_ea\":\"{MdeAlgorithm}_v2\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_PropertyNameWithSimilarSuffix_NotMistakenForEi_ReturnsNotEncrypted()
        {
            // "_eix" is not "_ei" — detector must not match.
            string json = $"{{\"_eix\":{{\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.NotEncrypted, Detect(json));
        }

        [TestMethod]
        public void Detect_EaWithEscapeSequence_LegacyDetected()
        {
            // _ea value contains escape — must still match the literal legacy name.
            // The legacy name has no special chars but we exercise the unescape path with a benign \u006e (n) escape.
            string json = "{\"_ei\":{\"_ea\":\"AEAes256CbcHmacSha256Ra\\u006edomized\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_EiPropertyNameWithEscape_LegacyDetected()
        {
            // Escaped underscore in property name (rare but legal: \u005f) — detector must still recognize it.
            string json = $"{{\"\\u005fei\":{{\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        // ============================================================================
        // Adversary-audit additions: edge cases that must remain backwards-compatible.
        // Each test documents what input the detector accepts/rejects, with the
        // invariant that any Unknown/non-positive classification falls through to
        // the JObject path (which existed pre-PR), preserving the pre-PR contract.
        // ============================================================================

        [TestMethod]
        public void Detect_TopLevelNullLiteral_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("null"));
        }

        [TestMethod]
        public void Detect_TopLevelTrueLiteral_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("true"));
        }

        [TestMethod]
        public void Detect_TopLevelFalseLiteral_ReturnsUnknown()
        {
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect("false"));
        }

        [TestMethod]
        public void Detect_DuplicateEiAtRoot_FirstWins_MdeDetected()
        {
            // JSON spec is ambiguous on duplicate top-level keys. Utf8JsonReader is forward-only,
            // so the detector consumes the FIRST `_ei` and ignores the rest. Newtonsoft.Linq.JObject
            // by default LAST-wins. This test documents the detector's first-wins behaviour and
            // pairs with Parity_PlainObject_DuplicateEiAtRoot which asserts both paths agree on
            // the resulting decrypt outcome (both must throw or both must succeed identically).
            string json = $"{{\"_ei\":{{\"_ea\":\"{MdeAlgorithm}\"}},\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.MdeAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_DuplicateEaInsideEi_FirstWins_MdeDetected()
        {
            // Same first-wins semantics inside `_ei`. Paired with parity test.
            string json = $"{{\"_ei\":{{\"_ea\":\"{MdeAlgorithm}\",\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.MdeAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_Utf8BomPrefix_ReturnsUnknown()
        {
            // Utf8JsonReader does NOT auto-skip BOM (it's only stripped by JsonDocument/Utf8JsonWriter helpers).
            // BOM-prefixed payloads must classify as Unknown so the JObject path (which is tolerant) handles them.
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            string json = $"{{\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            byte[] payload = new byte[bom.Length + json.Length];
            System.Buffer.BlockCopy(bom, 0, payload, 0, bom.Length);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(json);
            System.Buffer.BlockCopy(jsonBytes, 0, payload, bom.Length, jsonBytes.Length);
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, LegacyAlgorithmDetector.Detect(payload));
        }

        [TestMethod]
        public void Detect_DecoyRootLevelEa_BeforeRealEi_LegacyDetected()
        {
            // A root-level `_ea` (decoy) must be ignored — only `_ea` INSIDE the top-level `_ei` counts.
            string json = $"{{\"_ea\":\"decoy-at-root\",\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_EaWithLeadingWhitespace_NotMatched_ReturnsUnknown()
        {
            string json = $"{{\"_ei\":{{\"_ea\":\" {LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_EaWithTrailingWhitespace_NotMatched_ReturnsUnknown()
        {
            string json = $"{{\"_ei\":{{\"_ea\":\"{LegacyAlgorithm} \"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_EaWithInternalSpace_NotMatched_ReturnsUnknown()
        {
            string json = "{\"_ei\":{\"_ea\":\"AEAes256Cbc HmacSha256Randomized\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_EaWithLeadingTab_NotMatched_ReturnsUnknown()
        {
            string json = $"{{\"_ei\":{{\"_ea\":\"\\t{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_NearMissPropertyInEi_Eaa_ReturnsUnknown()
        {
            string json = $"{{\"_ei\":{{\"_eaa\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_NearMissPropertyInEi_DoubleUnderscoreEa_ReturnsUnknown()
        {
            string json = $"{{\"_ei\":{{\"__ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_InvalidUtf8Bytes_ReturnsUnknown()
        {
            byte[] invalid = new byte[] { 0xFF, 0xFE, 0xFD, 0xFC };
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, LegacyAlgorithmDetector.Detect(invalid));
        }

        [TestMethod]
        public void Detect_MdeAlgorithmAllUppercase_NotMatched_ReturnsUnknown()
        {
            // ValueTextEquals is ordinal/case-sensitive; the wire constant is mixed-case.
            string json = "{\"_ei\":{\"_ea\":\"MDEAEADAES256CBCHMAC256RANDOMIZED\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.Unknown, Detect(json));
        }

        [TestMethod]
        public void Detect_MdeAlgorithm_FirstThreeCharactersEscaped_MdeDetected()
        {
            // "Mde" each character as a unicode escape resolves to the same UTF-8 bytes;
            // ValueTextEquals normalises escapes per the STJ spec, so the match must hold.
            string json = "{\"_ei\":{\"_ea\":\"\\u004d\\u0064\\u0065AeadAes256CbcHmac256Randomized\"}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.MdeAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_ManyPropertiesBeforeEi_LegacyDetected()
        {
            // Stress: 1000 trivial properties before `_ei`. Detector must skip them in O(N) time
            // without hitting Utf8JsonReader depth limits or O(N^2) behaviour.
            System.Text.StringBuilder sb = new ("{");
            for (int i = 0; i < 1000; i++)
            {
                sb.Append("\"k").Append(i).Append("\":\"v").Append(i).Append("\",");
            }

            sb.Append("\"_ei\":{\"_ea\":\"").Append(LegacyAlgorithm).Append("\"}}");
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(sb.ToString()));
        }

        [TestMethod]
        public void Detect_VeryLargeEdValueBeforeEa_LegacyDetected()
        {
            // A large `_ed` (sibling of `_ea` inside `_ei`) must be efficiently skipped by reader.Skip()
            // without allocating an intermediate buffer that scales with the value length.
            string bulk = new ('A', 1_000_000);
            string json = $"{{\"_ei\":{{\"_ed\":\"{bulk}\",\"_ea\":\"{LegacyAlgorithm}\"}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }

        [TestMethod]
        public void Detect_RepeatedCalls_AreIdempotent()
        {
            string json = $"{{\"_ei\":{{\"_ea\":\"{MdeAlgorithm}\"}}}}";
            LegacyAlgorithmDetector.DetectionResult r1 = Detect(json);
            LegacyAlgorithmDetector.DetectionResult r2 = Detect(json);
            LegacyAlgorithmDetector.DetectionResult r3 = Detect(json);
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.MdeAlgorithm, r1);
            Assert.AreEqual(r1, r2);
            Assert.AreEqual(r1, r3);
        }

        [TestMethod]
        public void Detect_NestedEiInsideEd_OuterEaWins()
        {
            // `_ei._ed` happens to be a nested object that itself contains an `_ei._ea` looking like MDE.
            // The detector must classify based on the OUTER `_ea` (the real algorithm), not the decoy.
            string json = $"{{\"_ei\":{{\"_ea\":\"{LegacyAlgorithm}\",\"_ed\":{{\"_ei\":{{\"_ea\":\"{MdeAlgorithm}\"}}}}}}}}";
            Assert.AreEqual(LegacyAlgorithmDetector.DetectionResult.LegacyAlgorithm, Detect(json));
        }
    }
#pragma warning restore CS0618 // Type or member is obsolete
}
#endif
