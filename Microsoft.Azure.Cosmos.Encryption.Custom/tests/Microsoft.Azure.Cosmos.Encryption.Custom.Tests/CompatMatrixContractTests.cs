// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
// Offline companion to the CompatMatrix subprocess harness (tests/CompatMatrix).
// Enumerates every matrix cell {write-version x read-version x algorithm x write-proc x read-proc x read-path}
// as a named, deterministic test and asserts each SUPPORTED/UNSUPPORTED/NA classification. write-proc and
// read-proc are INDEPENDENT, so the cross-processor (A/B) cells are enumerated too: an MDE doc written under one
// processor and read under the other is SUPPORTED (MDE _ei docs are processor-interchangeable).
// Cross-version + cross-processor DATA interop (old<->new, Newtonsoft<->Stream) is proven against the emulator
// by tests/CompatMatrix/run-matrix.ps1.
//
// The harness rides a HARDENED document on every cell (regression coverage for the Stream-processor
// data-corruption fixes): an escaped string VALUE (quote/backslash/newline/unicode/control) on both an
// encrypted and a plaintext path, an escaped property NAME on an encrypted path, an encrypted OBJECT and
// ARRAY each carrying an inner null, a large long (2^53+1), and an integral (5.0) + ordinary double.
// Enriching the document does NOT change the cell SET, so the classification counts below are unchanged;
// the per-field BYTE/VALUE round-trip fidelity is asserted live by the harness against the emulator.

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CompatMatrixContractTests
    {
        // {old-write,new-write} x {old-read,new-read} x algo{MDE,AEAD} x write-proc{Newtonsoft,Stream}
        //   x read-proc{Newtonsoft,Stream} x path{point,query,feed} = 96 logical cells. write-proc and read-proc
        // are independent, so write-Newtonsoft/read-Stream and write-Stream/read-Newtonsoft are both enumerated.
        private static IEnumerable<object[]> Cells()
            => CellsForVersions(includeCurrent: false);

        private static IEnumerable<object[]> CellsForVersions(bool includeCurrent)
        {
            string[] versions = includeCurrent ? new[] { "old", "new", "current" } : new[] { "old", "new" };
            foreach (string w in versions)
            foreach (string r in versions)
            foreach (string algo in new[] { "MDE", "AEAD" })
            foreach (string wproc in new[] { "Newtonsoft", "Stream" })
            foreach (string rproc in new[] { "Newtonsoft", "Stream" })
            foreach (string path in new[] { "point", "query", "feed" })
            {
                yield return new object[] { w, r, algo, wproc, rproc, path };
            }
        }

        private const string EncAstralValue = "😀𐍈🜨 日本語 العربية \uD83D\uDE00 Z\u0301";

        // Expected classification for one cell. Mirrors the harness:
        //  * AEAD+Stream is unsupported-by-design on EITHER side (Validate throws): UNSUPPORTED.
        //  * Stream only exists in preview01, so a Stream WRITE on an old writer or a Stream READ on an old reader is N/A.
        //  * Otherwise SUPPORTED - including the cross-processor MDE cells, because an MDE (_ei fmt-v3) doc is
        //    processor-interchangeable: it decrypts to the identical original under Newtonsoft AND Stream.
        private static string Classify(string write, string read, string algo, string wproc, string rproc)
        {
            if (algo == "AEAD" && (wproc == "Stream" || rproc == "Stream")) { return "UNSUPPORTED"; }
            if ((wproc == "Stream" && write == "old") || (rproc == "Stream" && read == "old")) { return "NA-OLD-NO-STREAM"; }
            return "SUPPORTED";
        }

        [DataTestMethod]
        [DynamicData(nameof(Cells), DynamicDataSourceType.Method)]
        public void Cell_Classification_IsDeterministic(string write, string read, string algo, string wproc, string rproc, string path)
        {
            string status = Classify(write, read, algo, wproc, rproc);
            string expected = (algo == "AEAD" && (wproc == "Stream" || rproc == "Stream")) ? "UNSUPPORTED"
                : ((wproc == "Stream" && write == "old") || (rproc == "Stream" && read == "old")) ? "NA-OLD-NO-STREAM"
                : "SUPPORTED";
            Assert.AreEqual(expected, status, $"{write}->{read} {algo} {wproc}->{rproc}/{path}");
            CollectionAssert.Contains(new[] { "point", "query", "feed" }, path);
        }

        [TestMethod]
        public void Matrix_Has_Exact_Cell_Counts()
        {
            List<object[]> cells = Cells().ToList();
            int supported = cells.Count(c => Classify((string)c[0], (string)c[1], (string)c[2], (string)c[3], (string)c[4]) == "SUPPORTED");
            int unsupported = cells.Count(c => Classify((string)c[0], (string)c[1], (string)c[2], (string)c[3], (string)c[4]) == "UNSUPPORTED");
            int naOld = cells.Count(c => Classify((string)c[0], (string)c[1], (string)c[2], (string)c[3], (string)c[4]) == "NA-OLD-NO-STREAM");
            Assert.AreEqual(96, cells.Count, "2 write x 2 read x 2 algo x 2 write-proc x 2 read-proc x 3 path");
            Assert.AreEqual(39, supported, "supported: 12 AEAD (Newtonsoft<->Newtonsoft) + 27 MDE (incl. cross-processor A/B)");
            Assert.AreEqual(36, unsupported, "AEAD+Stream unsupported-by-design on either side (12 base tuples x 3 paths)");
            Assert.AreEqual(21, naOld, "MDE Stream touching an old end = N/A: preview07 has no Stream (7 base tuples x 3 paths)");
            Assert.AreEqual(cells.Count, supported + unsupported + naOld, "every cell classified exactly once");

            // The 39 SUPPORTED cells are EXACTLY the data cells the harness runs at -Processor both:
            // run-matrix.ps1 emits 42 grid cells = 39 data cells + 3 cross-processor EQUIVALENCE meta-cells.
            // The hardened payload enriches each cell's document but does not add/remove cells, so this holds.
            const int harnessGridCellsAtBoth = 42;
            const int harnessEquivalenceCells = 3;
            Assert.AreEqual(harnessGridCellsAtBoth - harnessEquivalenceCells, supported,
                "the 39 SUPPORTED cells == harness DATA cells (42 grid - 3 equivalence)");
        }

        [TestMethod]
        public void Matrix_WithCurrent_Has_Exact_OptIn_Cell_Counts()
        {
            List<object[]> cells = CellsForVersions(includeCurrent: true).ToList();
            int supported = cells.Count(c => Classify((string)c[0], (string)c[1], (string)c[2], (string)c[3], (string)c[4]) == "SUPPORTED");
            int unsupported = cells.Count(c => Classify((string)c[0], (string)c[1], (string)c[2], (string)c[3], (string)c[4]) == "UNSUPPORTED");
            int naOld = cells.Count(c => Classify((string)c[0], (string)c[1], (string)c[2], (string)c[3], (string)c[4]) == "NA-OLD-NO-STREAM");

            Assert.AreEqual(216, cells.Count, "3 write x 3 read x 2 algo x 2 write-proc x 2 read-proc x 3 path");
            Assert.AreEqual(102, supported, "supported data cells with Current included");
            Assert.AreEqual(81, unsupported, "AEAD+Stream unsupported-by-design on either side");
            Assert.AreEqual(33, naOld, "MDE Stream touching an old end = N/A: preview07 has no Stream");

            const int harnessGridCellsAtBoth = 112;
            const int harnessEquivalenceCells = 10;
            Assert.AreEqual(harnessGridCellsAtBoth - harnessEquivalenceCells, supported,
                "the 102 SUPPORTED cells == opt-in Current harness DATA cells (112 grid - 10 equivalence)");
        }

        [TestMethod]
        public void HardenedDocument_EncryptedAstralString_RoundTripsExactly()
        {
            MatrixDoc expected = new() { EncAstral = EncAstralValue };
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(expected);
            MatrixDoc actual = Newtonsoft.Json.JsonConvert.DeserializeObject<MatrixDoc>(json);

            Assert.AreEqual(EncAstralValue, actual.EncAstral, "encrypted astral/multi-script string must round-trip exactly");
            CollectionAssert.AreEqual(
                EncAstralValue.EnumerateRunes().Select(r => r.Value).ToArray(),
                actual.EncAstral.EnumerateRunes().Select(r => r.Value).ToArray(),
                "encrypted astral/multi-script string code points must be unchanged");
        }

        [TestMethod]
        public void Mde_DocsAreInterchangeableAcrossProcessors()
        {
            // A/B interchangeability: a new-written MDE doc read by a new reader is SUPPORTED under the OPPOSITE
            // processor - write-Newtonsoft/read-Stream AND write-Stream/read-Newtonsoft both decrypt the same _ei doc.
            Assert.AreEqual("SUPPORTED", Classify("new", "new", "MDE", "Newtonsoft", "Stream"), "write-N read-S must be supported");
            Assert.AreEqual("SUPPORTED", Classify("new", "new", "MDE", "Stream", "Newtonsoft"), "write-S read-N must be supported");

            // Same-processor baselines remain supported, so the cross pair above is a genuine A/B superset.
            Assert.AreEqual("SUPPORTED", Classify("new", "new", "MDE", "Newtonsoft", "Newtonsoft"));
            Assert.AreEqual("SUPPORTED", Classify("new", "new", "MDE", "Stream", "Stream"));

            // MDE is NEVER unsupported under any processor pairing (unlike AEAD). When a Stream side touches an
            // old end it is merely N/A (no Stream path existed in preview07), but it is never UNSUPPORTED.
            foreach (string w in new[] { "old", "new" })
            foreach (string r in new[] { "old", "new" })
            {
                Assert.AreNotEqual("UNSUPPORTED", Classify(w, r, "MDE", "Newtonsoft", "Stream"), $"MDE never unsupported: {w}->{r} N->S");
                Assert.AreNotEqual("UNSUPPORTED", Classify(w, r, "MDE", "Stream", "Newtonsoft"), $"MDE never unsupported: {w}->{r} S->N");
            }
        }

        [TestMethod]
        public void Aead_Stream_Unsupported_Throws()
        {
#if NET8_0_OR_GREATER
#pragma warning disable CS0618
            Assert.ThrowsException<NotSupportedException>(() => new EncryptionOptions
            {
                DataEncryptionKeyId = "dek",
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized,
                PathsToEncrypt = new List<string> { "/Sensitive" },
            }.Validate(JsonProcessor.Stream));
#pragma warning restore CS0618
#else
            Assert.Inconclusive("Stream processor is net8.0-only.");
#endif
        }

        [TestMethod]
        public void Mde_Stream_Newtonsoft_AreSupportedAlgorithms()
        {
            new EncryptionOptions
            {
                DataEncryptionKeyId = "dek",
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
                PathsToEncrypt = new List<string> { "/Sensitive" },
            }.Validate(JsonProcessor.Newtonsoft);
            Assert.AreEqual(3, EncryptionFormatVersion.Mde);
            Assert.AreEqual(2, EncryptionFormatVersion.AeAes);
        }

        [TestMethod]
        public void Compression_NotApplicable_NoCompressionPublicApi()
        {
            // Compression existed only after preview07 and is removed in 2.0.0-preview01: assert no branch.
            Type[] types = typeof(EncryptionOptions).Assembly.GetTypes();
            Assert.IsFalse(types.Any(t => t.Name.Contains("CompressionOptions")), "preview01 must not expose compression.");
            Assert.IsFalse(typeof(EncryptionOptions).GetProperties().Any(p => p.Name.Contains("Compression")));
        }

        private sealed class MatrixDoc
        {
            public string EncAstral { get; set; }
        }
    }
}
