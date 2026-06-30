// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
// Compat-matrix subprocess. ONE binary per package version (see the two csproj).
//   write  : creates the encrypted documents for every supported cell
//   read   : reads the peer version's docs, asserts RAW=ciphertext+metadata AND decrypted==original
//   tamper : writes a plaintext doc and proves the raw assertion FAILS (anti-fake-green)
// Cells: algorithm {MDE=v3, AEAD=v2} x write-proc x read-proc {Newtonsoft, Stream(MDE-only,net8)} x readpath {point,query,feed}.
// Reads force the decrypt processor via the per-request override (--processor=Newtonsoft|Stream|both), so the Stream
// DECRYPT path actually runs and write/read processors can be mismatched (A/B). MDE docs are processor-interchangeable:
// reading the SAME _ei doc under Newtonsoft and Stream yields the identical original (asserted by an EQUIVALENCE cell).
// Cross-version interop is proven by: old-write + new-read AND new-write + old-read.
// A cell is PASS only when (a) the RAW stored /Sensitive is NOT the plaintext and _ei metadata
// (fmt v3 MDE / v2 AEAD) is present AND (b) the encrypting reader decrypts back to the original.

namespace CompatMatrix
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    public static class Program
    {
#if CEC_NEW
        public const string Version = "new";
#else
        public const string Version = "old";
#endif
        private const string StreamKey = "encryption-json-processor"; // JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey
        private const string MdeDekId = "matrix-mde-dek";
        private const string AeadDekId = "matrix-aead-dek";
        private const string Pk = "matrix-pk";
#pragma warning disable CS0618
        private static readonly string AeadAlgo = CosmosEncryptionAlgorithm.AEAes256CbcHmacSha256Randomized;
#pragma warning restore CS0618
        private static readonly string MdeAlgo = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized;

        public static async Task<int> Main(string[] args)
        {
            Dictionary<string, string> a = Parse(args);
            string role = a.GetValueOrDefault("role", "read");
            string peer = a.GetValueOrDefault("peer", Version == "new" ? "old" : "new");
            // A/B read toggle: which processor(s) decrypt a cell. Default 'both' decrypts every MDE doc
            // under Newtonsoft AND Stream (exercising the Stream DECRYPT path and the cross-processor
            // equivalence). 'newtonsoft'/'stream' force a single read processor. Unknown -> 'both'.
            string processor = a.GetValueOrDefault("processor", "both").Trim().ToLowerInvariant();
            if (processor != "newtonsoft" && processor != "stream" && processor != "both") { processor = "both"; }
            string db = a.GetValueOrDefault("db", "compat-matrix");
            string endpoint = a.GetValueOrDefault("endpoint", Environment.GetEnvironmentVariable("COSMOS_ENDPOINT") ?? "https://127.0.0.1:8081/");
            string key = a.GetValueOrDefault("key", Environment.GetEnvironmentVariable("COSMOS_KEY") ?? "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");

            CosmosClient client;
            Database database;
            Container item, enc;
            try
            {
                client = new CosmosClient(endpoint, key, new CosmosClientOptions
                {
                    ConnectionMode = ConnectionMode.Gateway,
                    LimitToEndpoint = true,
                    HttpClientFactory = () => new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true }),
                });
                database = await client.CreateDatabaseIfNotExistsAsync(db);
                item = (await database.CreateContainerIfNotExistsAsync("items", "/PK", 400)).Container;
                Container keyC = (await database.CreateContainerIfNotExistsAsync("keys", "/id", 400)).Container;
                CosmosDataEncryptionKeyProvider provider = new(new MatrixWrapProvider(), new MatrixKeyStoreProvider());
                await provider.InitializeAsync(database, keyC.Id);
                enc = item.WithEncryptor(new MatrixEncryptor(provider));
                if (role == "write")
                {
                    await TryCreateDek(provider, MdeDekId, MdeAlgo);
                    await TryCreateDek(provider, AeadDekId, AeadAlgo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SKIP|emulator-unreachable|{Version}|{ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
                return 2;
            }

            return role switch
            {
                "write" => await Write(enc, peer),
                "tamper" => await Tamper(item),
                _ => await Read(enc, item, peer, processor),
            };
        }

        private static IEnumerable<(string algo, string proc)> Cells()
        {
            yield return (MdeAlgo, "Newtonsoft");
            yield return (MdeAlgo, "Stream");
            yield return (AeadAlgo, "Newtonsoft");
            yield return (AeadAlgo, "Stream"); // unsupported-by-design: asserted to fail on write
        }

        private static async Task<int> Write(Container enc, string peer)
        {
            foreach ((string algo, string proc) in Cells())
            {
                string family = algo == MdeAlgo ? "MDE" : "AEAD";
                string id = $"cell-{family}-{proc}-by-{Version}";
                Doc d = new() { id = id, PK = Pk, NonSensitive = "plain", Sensitive = $"secret::{id}" };
                bool aeadStream = family == "AEAD" && proc == "Stream";
                bool mdeStreamOnOld = Version == "old" && proc == "Stream";
                try
                {
                    await enc.UpsertItemAsync(d, new PartitionKey(Pk), Options(algo, proc));
                    Console.WriteLine(aeadStream
                        ? (Version == "new"
                            ? $"WROTE|UNSUPPORTED-DID-NOT-THROW|{family}|{proc}|{id}"      // preview01 MUST reject AEAD+Stream
                            : $"WROTE|OLD-NO-STREAM-EXPECTED|{family}|{proc}|{id}")        // preview07 has no Stream -> AEAD via Newtonsoft (no-op, expected)
                        : $"WROTE|OK|{family}|{proc}|{id}");
                }
                catch (NotSupportedException) when (aeadStream)
                {
                    Console.WriteLine($"WROTE|EXPECTED-UNSUPPORTED|{family}|{proc}|AEAD+Stream rejected");
                }
                catch (Exception ex) when (mdeStreamOnOld)
                {
                    Console.WriteLine($"WROTE|OLD-NO-STREAM|{family}|{proc}|{ex.GetType().Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WROTE|FAIL|{family}|{proc}|{ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
                }
            }
            return 0;
        }

        // Reads every supported cell written by 'peer'. The doc id encodes the WRITE processor (wproc);
        // the READ processor (rproc) is chosen per the A/B 'processorToggle', so a Stream-written doc can be
        // read under Newtonsoft and a Newtonsoft-written doc under Stream. Default 'both' decrypts each MDE doc
        // under Newtonsoft AND Stream so the Stream DECRYPT path is exercised end-to-end and an EQUIVALENCE cell
        // proves both processors decrypt the SAME _ei doc to the IDENTICAL original (MDE is interchangeable).
        // The cipher+_ei(mde-v3 / aead-v2) raw assertion is kept unchanged.
        private static async Task<int> Read(Container enc, Container plain, string peer, string processorToggle)
        {
            int fails = 0;
            foreach ((string algo, string wproc) in Cells())
            {
                string family = algo == MdeAlgo ? "MDE" : "AEAD";
                if (family == "AEAD" && wproc == "Stream") { continue; }  // unsupported-by-design (never written/read)
                if (wproc == "Stream" && peer == "old") { continue; }     // preview07 never produced a stream-written doc
                string id = $"cell-{family}-{wproc}-by-{peer}";
                string expected = $"secret::{id}";

                // (a) RAW assertion: the stored doc must be ciphertext + carry _ei metadata, never plaintext.
                (bool rawOk, string rawDetail) = await RawIsEncrypted(plain, id, family, expected);

                // (b) decrypted round-trip must equal the original under EACH selected read processor.
                Dictionary<string, string> decryptedByProc = new();
                foreach (string rproc in ReadProcessors(family, processorToggle))
                {
                    foreach (string path in new[] { "point", "query", "feed" })
                    {
                        string label = $"CELL|{peer}-write|{Version}-read|{family}|{wproc}->{rproc}|{path}";
                        try
                        {
                            Doc r = await ReadPath(enc, path, id, rproc);
                            bool decOk = r != null && r.Sensitive == expected;
                            if (rawOk && decOk) { Console.WriteLine($"{label}|PASS|{rawDetail}"); decryptedByProc[rproc] = r.Sensitive; }
                            else
                            {
                                string why = !rawOk ? $"raw:{rawDetail}" : r == null ? "not-found" : $"mismatch '{r.Sensitive}'";
                                Console.WriteLine($"{label}|FAIL|{why}");
                                fails++;
                            }
                        }
                        catch (Exception ex) { Console.WriteLine($"{label}|FAIL|{ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); fails++; }
                    }
                }

                // (c) cross-processor EQUIVALENCE: the SAME _ei doc must decrypt to the IDENTICAL original under
                // both Newtonsoft and Stream (covers write-N read-S and write-S read-N), proving interchangeability.
                if (decryptedByProc.TryGetValue("Newtonsoft", out string viaNewtonsoft) &&
                    decryptedByProc.TryGetValue("Stream", out string viaStream))
                {
                    string label = $"CELL|{peer}-write|{Version}-read|{family}|{wproc}->A/B|equiv";
                    if (viaNewtonsoft == viaStream && viaNewtonsoft == expected) { Console.WriteLine($"{label}|PASS|N==S (interchangeable)"); }
                    else { Console.WriteLine($"{label}|FAIL|N='{viaNewtonsoft}' S='{viaStream}'"); fails++; }
                }
            }
            return fails == 0 ? 0 : 1;
        }

        // Read processors applied to a stored cell. AEAD is Newtonsoft-only (AEAD+Stream is unsupported-by-design);
        // MDE honors the --processor toggle. Stream DECRYPT only exists in preview01, so an OLD reader stays Newtonsoft.
        private static IEnumerable<string> ReadProcessors(string family, string toggle)
        {
            if (family == "AEAD") { yield return "Newtonsoft"; yield break; }
            bool readerSupportsStream = Version == "new";
            switch (toggle)
            {
                case "newtonsoft":
                    yield return "Newtonsoft";
                    break;
                case "stream":
                    yield return readerSupportsStream ? "Stream" : "Newtonsoft"; // OLD has no Stream path; MDE is interchangeable
                    break;
                default: // "both"
                    yield return "Newtonsoft";
                    if (readerSupportsStream) { yield return "Stream"; }
                    break;
            }
        }

        // Reads the stored document via a NON-encrypting container and proves it is real ciphertext:
        //   - _ei metadata present with the expected format version (MDE=v3, AEAD=v2),
        //   - /Sensitive is NOT the original plaintext (MDE: in-place cipher; AEAD: path removed + _ed cipher).
        // Returns false (with reason) for a silent no-op / plaintext / metadata-stripped doc.
        private static async Task<(bool ok, string detail)> RawIsEncrypted(Container plain, string id, string family, string expectedPlain)
        {
            JObject raw;
            try { raw = (await plain.ReadItemAsync<JObject>(id, new PartitionKey(Pk))).Resource; }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound) { return (false, "raw-not-found"); }
            if (raw["_ei"] is not JObject ei) { return (false, "no-_ei-metadata"); }
            int ver = ei.Value<int?>("_ef") ?? -1;
            int want = family == "MDE" ? 3 : 2;
            if (ver != want) { return (false, $"fmt-v{ver}-not-v{want}"); }
            JToken sens = raw["Sensitive"];
            if (family == "MDE")
            {
                if (sens == null || sens.Type == JTokenType.Null) { return (false, "MDE-Sensitive-stripped"); }
                if (sens.Type == JTokenType.String && sens.Value<string>() == expectedPlain) { return (false, "Sensitive-is-plaintext"); }
            }
            else
            {
                if (sens != null && sens.Type != JTokenType.Null) { return (false, "AEAD-plaintext-leaked"); }
                if (string.IsNullOrEmpty(ei.Value<string>("_ed"))) { return (false, "no-_ed-ciphertext"); }
            }
            return (true, $"cipher+v{ver}");
        }

        // Negative control: stores a plaintext doc (no encryption) and proves the raw assertion REJECTS it.
        // Exit 0 = tamper correctly caught; exit 1 = anti-fake-green guard failed.
        private static async Task<int> Tamper(Container plain)
        {
            string id = $"cell-MDE-Newtonsoft-tamper-by-{Version}";
            string expected = $"secret::{id}";
            await plain.UpsertItemAsync(new Doc { id = id, PK = Pk, NonSensitive = "plain", Sensitive = expected }, new PartitionKey(Pk));
            (bool ok, string detail) = await RawIsEncrypted(plain, id, "MDE", expected);
            Console.WriteLine(ok ? "TAMPER|FAIL|plaintext-passed-as-encrypted" : $"TAMPER|PASS|plaintext-rejected:{detail}");
            return ok ? 1 : 0;
        }

        // Reads the encrypted doc via point/query/feed, forcing the decrypt processor through the per-request
        // override (RequestOptions.Properties[encryption-json-processor]). On preview01 this selects the Stream or
        // Newtonsoft DECRYPT path; preview07 ignores the key and decrypts via its single (Newtonsoft) path.
        private static async Task<Doc> ReadPath(Container enc, string path, string id, string rproc)
        {
            Dictionary<string, object> props = new() { { StreamKey, rproc } };
            if (path == "point")
            {
                try { return (await enc.ReadItemAsync<Doc>(id, new PartitionKey(Pk), new ItemRequestOptions { Properties = props })).Resource; }
                catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound) { return null; }
            }
            string q = path == "query"
                ? "SELECT * FROM c WHERE c.id = @id"
                : "SELECT * FROM c";
            QueryDefinition qd = new QueryDefinition(q);
            if (path == "query") { qd = qd.WithParameter("@id", id); }
            using FeedIterator<Doc> it = enc.GetItemQueryIterator<Doc>(qd, requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(Pk), Properties = props });
            while (it.HasMoreResults)
            {
                foreach (Doc d in await it.ReadNextAsync()) { if (d.id == id) { return d; } }
            }
            return null;
        }

        private static EncryptionItemRequestOptions Options(string algo, string proc) => new()
        {
            EncryptionOptions = new EncryptionOptions
            {
                DataEncryptionKeyId = algo == MdeAlgo ? MdeDekId : AeadDekId,
                EncryptionAlgorithm = algo,
                PathsToEncrypt = new List<string> { "/Sensitive" },
            },
            Properties = new Dictionary<string, object> { { StreamKey, proc } },
        };

        private static async Task TryCreateDek(CosmosDataEncryptionKeyProvider p, string id, string algo)
        {
            try
            {
                await p.DataEncryptionKeyContainer.CreateDataEncryptionKeyAsync(id, algo, new Microsoft.Azure.Cosmos.Encryption.Custom.EncryptionKeyWrapMetadata("matrix-store", "https://matrix.local"));
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.Conflict) { }
        }

        private static Dictionary<string, string> Parse(string[] args)
        {
            Dictionary<string, string> d = new();
            foreach (string s in args)
            {
                int i = s.IndexOf('=');
                if (s.StartsWith("--") && i > 0) { d[s.Substring(2, i - 2)] = s.Substring(i + 1); }
            }
            return d;
        }

        private sealed class Doc
        {
            public string id { get; set; }
            public string PK { get; set; }
            public string NonSensitive { get; set; }
            public string Sensitive { get; set; }
        }
    }
}
