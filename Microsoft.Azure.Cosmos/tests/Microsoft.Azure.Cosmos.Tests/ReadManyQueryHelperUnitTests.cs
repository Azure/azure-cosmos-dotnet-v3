//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.CosmosElements;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Unit tests for the three <c>internal static</c> helpers that the ReadMany
    /// point-read fast path (PR #5905) leans on:
    ///
    ///   * <c>ReadManyQueryHelper.ReadStreamAsCosmosElementAsync</c> — converts a
    ///     <see cref="ResponseMessage.Content"/> into a <see cref="CosmosElement"/>,
    ///     with a zero-copy fast path for publicly-visible
    ///     <see cref="MemoryStream"/> inputs and a <see cref="Stream.CopyToAsync(Stream, int, CancellationToken)"/>
    ///     fallback for everything else.
    ///
    ///   * <c>ReadManyQueryHelper.TryGetContainerRidFromDocument</c> — derives the
    ///     parent container's RID from a document's <c>_rid</c> field, mirroring
    ///     the canonical extraction used by <c>CollectionCache</c> and
    ///     <c>NetworkAttachedDocumentContainer</c>.
    ///
    ///   * <c>ReadManyQueryHelper.BuildSyntheticQueryResponseHeaders</c> — shapes
    ///     the <see cref="CosmosQueryResponseMessageHeaders"/> handed to
    ///     <c>QueryResponse.CreateSuccess</c> / <c>QueryResponse.CreateFailure</c>
    ///     on the per-partition fast-path branches; pins the null-containerRid
    ///     coalesce that protects the captured <c>Lazy&lt;MemoryStream&gt;</c>
    ///     from a latent NRE on downstream realization.
    ///
    /// The .NET <see cref="Stream"/> API has subtle gotchas around
    /// <see cref="MemoryStream.TryGetBuffer(out ArraySegment{byte})"/> success vs.
    /// fallback, current-position semantics, and cancellation propagation. This
    /// suite pins the contract so any future refactor of either helper is forced
    /// to re-evaluate the asymmetries (rather than break them silently).
    /// </summary>
    [TestClass]
    public class ReadManyQueryHelperUnitTests
    {
        // A known-good Cosmos document resource id (24-char base64), reused
        // elsewhere in the tests project — see InMemoryContainer.cs. We derive the
        // expected parent container RID from the same string via the canonical
        // ResourceId.Parse(...).DocumentCollectionId.ToString() pipeline so the
        // test asserts the *equivalence* with that pipeline, not a hardcoded
        // base64 string that could drift if the underlying ResourceId encoding
        // ever changes.
        private const string SampleDocumentRid = "AYIMAMmFOw8YAAAAAAAAAA==";

        private static readonly string ExpectedContainerRid =
            ResourceId.Parse(SampleDocumentRid).DocumentCollectionId.ToString();

        private static readonly string SampleDocumentJson =
            "{\"id\":\"a\",\"pk\":\"x\",\"_rid\":\"" + SampleDocumentRid + "\"}";

        // -----------------------------------------------------------------
        //  ReadStreamAsCosmosElementAsync — null & empty inputs
        // -----------------------------------------------------------------

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_NullStream_ReturnsNull()
        {
            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: null,
                cancellationToken: CancellationToken.None);

            Assert.IsNull(result, "null stream must short-circuit to null without invoking the JSON parser.");
        }

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_EmptyMemoryStream_ReturnsNull()
        {
            using MemoryStream empty = new MemoryStream();

            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: empty,
                cancellationToken: CancellationToken.None);

            Assert.IsNull(result, "MemoryStream with Length == 0 must return null without invoking the JSON parser.");
        }

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_EmptyNonMemoryStream_ReturnsNull()
        {
            using Stream empty = new ChunkedReadOnlyStream(Array.Empty<byte>(), chunkSize: 8);

            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: empty,
                cancellationToken: CancellationToken.None);

            Assert.IsNull(result, "non-MemoryStream that copies zero bytes must return null without invoking the JSON parser.");
        }

        // -----------------------------------------------------------------
        //  ReadStreamAsCosmosElementAsync — MemoryStream variants
        // -----------------------------------------------------------------

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_PubliclyVisibleMemoryStream_UsesTryGetBufferFastPath()
        {
            byte[] body = Encoding.UTF8.GetBytes(SampleDocumentJson);

            // Default ctor + Write produces a publiclyVisible MemoryStream — the
            // zero-copy TryGetBuffer fast path.
            using MemoryStream publicMs = new MemoryStream();
            publicMs.Write(body, 0, body.Length);
            publicMs.Position = 0;

            Assert.IsTrue(
                publicMs.TryGetBuffer(out _),
                "test fixture invariant: the default-ctor MemoryStream must be publiclyVisible so the TryGetBuffer fast path is exercised.");

            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: publicMs,
                cancellationToken: CancellationToken.None);

            AssertElementMatchesSampleDocument(result);
        }

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_PrivateBufferMemoryStream_FallsBackToToArray()
        {
            byte[] body = Encoding.UTF8.GetBytes(SampleDocumentJson);

            // new MemoryStream(byte[]) → publiclyVisible == false → TryGetBuffer returns
            // false → exercises the ToArray() fallback path.
            using MemoryStream privateMs = new MemoryStream(body);

            Assert.IsFalse(
                privateMs.TryGetBuffer(out _),
                "test fixture invariant: a byte[]-ctor MemoryStream must be non-publiclyVisible so the ToArray fallback is exercised.");

            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: privateMs,
                cancellationToken: CancellationToken.None);

            AssertElementMatchesSampleDocument(result);
        }

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_MemoryStreamAtEndPosition_StillReturnsFullContent()
        {
            // Contract pin: on the MemoryStream fast path the helper does NOT rewind
            // and does NOT honor the current Position. TryGetBuffer and ToArray
            // both return the full underlying buffer (offset 0, count Length), so
            // a stream already consumed by an upstream diagnostics pass is still
            // parseable. If a future refactor introduces "read from Position", it
            // will break the cached-response reuse pattern downstream — this test
            // catches that.
            byte[] body = Encoding.UTF8.GetBytes(SampleDocumentJson);
            using MemoryStream ms = new MemoryStream();
            ms.Write(body, 0, body.Length);
            ms.Position = ms.Length;

            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: ms,
                cancellationToken: CancellationToken.None);

            AssertElementMatchesSampleDocument(result);
        }

        // -----------------------------------------------------------------
        //  ReadStreamAsCosmosElementAsync — non-MemoryStream variants
        // -----------------------------------------------------------------

        [DataTestMethod]
        [DataRow(8, DisplayName = "8-byte chunks: forces multi-chunk CopyToAsync")]
        [DataRow(1, DisplayName = "1-byte chunks: stress the loop reassembly")]
        [DataRow(81920, DisplayName = "Single-chunk: whole body in one Read")]
        public async Task ReadStreamAsCosmosElementAsync_NonMemoryStream_VariousChunkSizes_ParsesCorrectly(int chunkSize)
        {
            byte[] body = Encoding.UTF8.GetBytes(SampleDocumentJson);
            using Stream nonMs = new ChunkedReadOnlyStream(body, chunkSize);

            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: nonMs,
                cancellationToken: CancellationToken.None);

            AssertElementMatchesSampleDocument(result);
        }

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_NonMemoryStreamAtNonZeroPosition_ReadsFromCurrentPosition()
        {
            // Contract pin: on the non-MemoryStream branch the helper CopyToAsync's
            // from the stream's current Position to EOF (the standard CopyToAsync
            // contract — it does NOT rewind for the caller). This deliberately
            // asymmetric behavior vs. the MemoryStream branch is worth pinning so
            // a future "unify the two branches" refactor cannot regress either side
            // silently.
            byte[] prefix = Encoding.UTF8.GetBytes("THROW_AWAY_PREFIX");
            byte[] body = Encoding.UTF8.GetBytes(SampleDocumentJson);
            byte[] composite = new byte[prefix.Length + body.Length];
            Buffer.BlockCopy(prefix, 0, composite, 0, prefix.Length);
            Buffer.BlockCopy(body, 0, composite, prefix.Length, body.Length);

            using Stream nonMs = new ChunkedReadOnlyStream(composite, chunkSize: 8);
            nonMs.Position = prefix.Length;

            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: nonMs,
                cancellationToken: CancellationToken.None);

            AssertElementMatchesSampleDocument(result);
        }

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_NonMemoryStream_PreCancelledToken_Throws()
        {
            // Contract pin: cancellation IS honored on the non-MemoryStream branch
            // because the helper hands the token to CopyToAsync. The token-check
            // happens at the first await, so a pre-cancelled token reliably
            // surfaces before any bytes are consumed.
            byte[] body = Encoding.UTF8.GetBytes(SampleDocumentJson);
            using Stream nonMs = new ChunkedReadOnlyStream(body, chunkSize: 8);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                async () => await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                    stream: nonMs,
                    cancellationToken: cts.Token));
        }

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_MemoryStream_PreCancelledToken_DoesNotThrow()
        {
            // Contract pin (current behavior, not necessarily ideal): the MemoryStream
            // fast path performs NO async operations, so the cancellation token is
            // never consulted. Even a pre-cancelled token will not interrupt parsing
            // of an in-memory body. Captured as a test so any future "honor
            // cancellation everywhere" change is forced to be deliberate, with
            // visible test churn rather than a silent behavior shift.
            byte[] body = Encoding.UTF8.GetBytes(SampleDocumentJson);
            using MemoryStream ms = new MemoryStream(body);

            using CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: ms,
                cancellationToken: cts.Token);

            AssertElementMatchesSampleDocument(result);
        }

        // -----------------------------------------------------------------
        //  ReadStreamAsCosmosElementAsync — JSON shape variants
        // -----------------------------------------------------------------

        [DataTestMethod]
        [DataRow("{\"id\":\"a\",\"value\":42}", typeof(CosmosObject), DisplayName = "JSON object")]
        [DataRow("[1,2,3]", typeof(CosmosArray), DisplayName = "JSON array")]
        [DataRow("42", typeof(CosmosNumber), DisplayName = "JSON number")]
        [DataRow("\"hello\"", typeof(CosmosString), DisplayName = "JSON string")]
        [DataRow("true", typeof(CosmosBoolean), DisplayName = "JSON bool (true)")]
        [DataRow("false", typeof(CosmosBoolean), DisplayName = "JSON bool (false)")]
        [DataRow("null", typeof(CosmosNull), DisplayName = "JSON null")]
        public async Task ReadStreamAsCosmosElementAsync_VariousJsonShapes_ParsedCorrectly(
            string json,
            Type expectedElementType)
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            using MemoryStream ms = new MemoryStream(body);

            CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                stream: ms,
                cancellationToken: CancellationToken.None);

            Assert.IsNotNull(result, $"non-empty JSON ({json}) must parse to a CosmosElement.");
            Assert.IsInstanceOfType(
                result,
                expectedElementType,
                $"JSON '{json}' must surface as a {expectedElementType.Name}.");
        }

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_DoesNotDisposeCallerOwnedMemoryStream()
        {
            // Contract pin: when the caller hands us a MemoryStream we MUST NOT dispose it
            // — the caller owns its lifetime (in the point-read path the outer
            // `using (pointReadResponse)` is responsible for disposing
            // ResponseMessage.Content). A disposed MemoryStream surfaces CanRead == false
            // and throws ObjectDisposedException on subsequent reads, so checking both is
            // a robust way to confirm the helper kept its hands off.
            byte[] body = Encoding.UTF8.GetBytes(SampleDocumentJson);
            MemoryStream ms = new MemoryStream(body);

            try
            {
                CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                    stream: ms,
                    cancellationToken: CancellationToken.None);

                AssertElementMatchesSampleDocument(result);

                Assert.IsTrue(ms.CanRead, "caller-owned MemoryStream must still be readable after the helper returns; the helper must not dispose it.");
                Assert.AreEqual(body.Length, ms.Length, "caller-owned MemoryStream content must remain intact after the helper returns.");

                ms.Position = 0;
                int firstByte = ms.ReadByte();
                Assert.AreEqual(body[0], (byte)firstByte, "caller-owned MemoryStream must still support Read after the helper returns (i.e., it must not have been disposed).");
            }
            finally
            {
                ms.Dispose();
            }
        }

        [TestMethod]
        public async Task ReadStreamAsCosmosElementAsync_DoesNotDisposeCallerOwnedNonMemoryStream()
        {
            // Contract pin: the caller's non-MemoryStream input is also not the helper's
            // to dispose — the internal CopyToAsync destination is owned by the helper,
            // but the source stream is the caller's responsibility.
            byte[] body = Encoding.UTF8.GetBytes(SampleDocumentJson);
            ChunkedReadOnlyStream nonMs = new ChunkedReadOnlyStream(body, chunkSize: 8);

            try
            {
                CosmosElement result = await ReadManyQueryHelper.ReadStreamAsCosmosElementAsync(
                    stream: nonMs,
                    cancellationToken: CancellationToken.None);

                AssertElementMatchesSampleDocument(result);

                Assert.IsTrue(nonMs.CanRead, "caller-owned non-MemoryStream must still be readable after the helper returns; the helper must not dispose it.");
            }
            finally
            {
                nonMs.Dispose();
            }
        }

        // -----------------------------------------------------------------
        //  TryGetContainerRidFromDocument — exhaustive matrix
        // -----------------------------------------------------------------

        [TestMethod]
        public void TryGetContainerRidFromDocument_NullElement_ReturnsNull()
        {
            string containerRid = ReadManyQueryHelper.TryGetContainerRidFromDocument(document: null);

            Assert.IsNull(containerRid, "null input must short-circuit to null (caller treats this as 'container RID unknown').");
        }

        [DataTestMethod]
        [DataRow("[1,2,3]", DisplayName = "JSON array (not an object)")]
        [DataRow("42", DisplayName = "JSON number (not an object)")]
        [DataRow("\"hello\"", DisplayName = "JSON string (not an object)")]
        [DataRow("true", DisplayName = "JSON bool (not an object)")]
        [DataRow("null", DisplayName = "JSON null (not an object)")]
        public void TryGetContainerRidFromDocument_NonObjectElement_ReturnsNull(string json)
        {
            CosmosElement notAnObject = CosmosElement.Parse(json);

            string containerRid = ReadManyQueryHelper.TryGetContainerRidFromDocument(notAnObject);

            Assert.IsNull(
                containerRid,
                $"non-object element ({json}) must yield null; only document-shaped objects expose a _rid field.");
        }

        [DataTestMethod]
        [DataRow("{\"id\":\"a\"}", DisplayName = "Object without _rid field")]
        [DataRow("{\"_rid\":42}", DisplayName = "_rid is a number (TryGetValue<CosmosString> fails)")]
        [DataRow("{\"_rid\":null}", DisplayName = "_rid is null (TryGetValue<CosmosString> fails)")]
        [DataRow("{\"_rid\":\"not a valid base64 cosmos rid\"}", DisplayName = "_rid is a malformed string (ResourceId.TryParse fails)")]
        [DataRow("{\"_rid\":\"\"}", DisplayName = "_rid is the empty string")]
        public void TryGetContainerRidFromDocument_InvalidRid_ReturnsNull(string json)
        {
            CosmosElement element = CosmosElement.Parse(json);

            string containerRid = ReadManyQueryHelper.TryGetContainerRidFromDocument(element);

            Assert.IsNull(
                containerRid,
                $"{json}: an invalid or missing _rid must yield null so callers fall back to 'container RID unknown' on the synthetic header.");
        }

        [TestMethod]
        public void TryGetContainerRidFromDocument_ValidDocumentRid_ReturnsContainerRid()
        {
            CosmosElement document = CosmosElement.Parse(
                "{\"id\":\"a\",\"pk\":\"x\",\"_rid\":\"" + SampleDocumentRid + "\"}");

            string containerRid = ReadManyQueryHelper.TryGetContainerRidFromDocument(document);

            Assert.IsNotNull(containerRid, "a valid document _rid must yield a non-null container RID.");
            Assert.AreEqual(
                ExpectedContainerRid,
                containerRid,
                "container RID extraction must match the canonical ResourceId.Parse(...).DocumentCollectionId.ToString() derivation used by CollectionCache and NetworkAttachedDocumentContainer.");
        }

        // -----------------------------------------------------------------
        //  BuildSyntheticQueryResponseHeaders — null-safety + field forwarding
        //
        //  The point-read fast path passes containerRid: null on the 404/Unknown
        //  and !IsSuccessStatusCode branches (ReadManyQueryHelper.cs lines 491 and
        //  506), and may also pass null on the success path when
        //  TryGetContainerRidFromDocument fails to derive a RID from a malformed
        //  _rid. QueryResponse.CreateSuccess captures the resulting ContainerRid
        //  inside a Lazy<MemoryStream>. If anything ever realizes that stream via
        //  QueryResponse.Content (diagnostics, logging, future middleware), the
        //  call chain CosmosElementSerializer.ToStream → IJsonWriter.WriteStringValue(null)
        //  → Encoding.UTF8.GetByteCount(null) throws ArgumentNullException deep in
        //  the JSON serializer. The helper coerces null → string.Empty so the
        //  latent NRE cannot fire on any code path.
        // -----------------------------------------------------------------

        [TestMethod]
        public void BuildSyntheticQueryResponseHeaders_NullContainerRid_IsCoercedToEmptyString()
        {
            ResponseMessage pointReadResponse = new ResponseMessage(System.Net.HttpStatusCode.NotFound);

            CosmosQueryResponseMessageHeaders headers = ReadManyQueryHelper.BuildSyntheticQueryResponseHeaders(
                pointReadResponse: pointReadResponse,
                containerRid: null);

            Assert.IsNotNull(headers.ContainerRid, "null containerRid must be coerced to a non-null sentinel so downstream Lazy<MemoryStream> realization is safe.");
            Assert.AreEqual(string.Empty, headers.ContainerRid, "null containerRid must be coerced to string.Empty (not any other sentinel) so JSON-serialized output remains shaped like the wire {\"_rid\":\"\",...} envelope on synthetic failure responses.");
        }

        [TestMethod]
        public void BuildSyntheticQueryResponseHeaders_NullContainerRid_QueryResponseContentMaterializationDoesNotThrow()
        {
            // Repro the exact chain described on the PR review:
            //   QueryResponse.CreateSuccess → Lazy<MemoryStream> capturing headers.ContainerRid
            //   → ResponseMessage.Content getter → CosmosElementSerializer.ToStream(null, ...)
            //   → IJsonWriter.WriteStringValue(null) → Encoding.UTF8.GetByteCount(null)
            //   → ArgumentNullException
            // Without the null-coalesce inside BuildSyntheticQueryResponseHeaders, the
            // assertion below catches the NRE; with the coalesce in place, the body is
            // a well-formed JSON envelope with _rid="" and an empty Documents array.
            ResponseMessage pointReadResponse = new ResponseMessage(System.Net.HttpStatusCode.NotFound);

            CosmosQueryResponseMessageHeaders headers = ReadManyQueryHelper.BuildSyntheticQueryResponseHeaders(
                pointReadResponse: pointReadResponse,
                containerRid: null);

            QueryResponse queryResponse = QueryResponse.CreateSuccess(
                result: Array.Empty<CosmosElement>(),
                count: 0,
                responseHeaders: headers,
                serializationOptions: null,
                trace: NoOpTrace.Singleton);

            using (queryResponse)
            {
                Stream content = queryResponse.Content;

                Assert.IsNotNull(content, "QueryResponse.Content must materialize the Lazy<MemoryStream> rather than surface null on the 404/Unknown branch.");

                // Round-trip the realized stream to confirm it is a valid JSON envelope —
                // a partial / truncated write would also raise here.
                using StreamReader reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
                string body = reader.ReadToEnd();

                Assert.IsTrue(body.Contains("\"_rid\":\"\""), $"realized envelope must contain an empty _rid field (coerced from null), got: {body}");
                Assert.IsTrue(body.Contains("\"_count\":0"), $"realized envelope must report a zero count on the empty 404/Unknown branch, got: {body}");
            }
        }

        [TestMethod]
        public void BuildSyntheticQueryResponseHeaders_NonNullContainerRid_IsPreservedExactly()
        {
            ResponseMessage pointReadResponse = new ResponseMessage(System.Net.HttpStatusCode.OK);

            CosmosQueryResponseMessageHeaders headers = ReadManyQueryHelper.BuildSyntheticQueryResponseHeaders(
                pointReadResponse: pointReadResponse,
                containerRid: ExpectedContainerRid);

            Assert.AreEqual(ExpectedContainerRid, headers.ContainerRid, "a non-null containerRid must flow through unmodified — coercion must only fire on null inputs.");
        }

        [TestMethod]
        public void BuildSyntheticQueryResponseHeaders_EmptyContainerRid_IsPreservedExactly()
        {
            // Empty-string in must remain empty-string out — the coalesce semantics are
            // strictly null → empty, not "falsey → empty". This pins the boundary so a
            // future refactor cannot silently widen the coalesce to e.g. IsNullOrWhiteSpace.
            ResponseMessage pointReadResponse = new ResponseMessage(System.Net.HttpStatusCode.OK);

            CosmosQueryResponseMessageHeaders headers = ReadManyQueryHelper.BuildSyntheticQueryResponseHeaders(
                pointReadResponse: pointReadResponse,
                containerRid: string.Empty);

            Assert.AreEqual(string.Empty, headers.ContainerRid, "an explicitly-empty containerRid must round-trip unchanged.");
        }

        [TestMethod]
        public void BuildSyntheticQueryResponseHeaders_ForwardsRequestChargeActivityIdAndSubStatusCode_FromUpstream()
        {
            // The helper exists precisely to mirror the wire response's SubStatusCode (so
            // non-404 failures surface their real diagnostic signal on the combined
            // ResponseMessage), and to forward RequestCharge and ActivityId so the
            // synthetic response is observationally equivalent to a direct
            // ReadItemStreamAsync call. Pin all three.
            string activityId = Guid.NewGuid().ToString();
            ResponseMessage pointReadResponse = new ResponseMessage(System.Net.HttpStatusCode.Gone);
            pointReadResponse.Headers.RequestCharge = 12.5;
            pointReadResponse.Headers.ActivityId = activityId;
            pointReadResponse.Headers.SubStatusCode = SubStatusCodes.PartitionKeyRangeGone;

            CosmosQueryResponseMessageHeaders headers = ReadManyQueryHelper.BuildSyntheticQueryResponseHeaders(
                pointReadResponse: pointReadResponse,
                containerRid: null);

            Assert.AreEqual(12.5, headers.RequestCharge, "RequestCharge must be forwarded so per-partition RU consumption rolls up into the combined ReadMany response.");
            Assert.AreEqual(activityId, headers.ActivityId, "ActivityId must be forwarded so request tracing on the synthetic response matches the upstream call.");
            Assert.AreEqual(SubStatusCodes.PartitionKeyRangeGone, headers.SubStatusCode, "SubStatusCode must mirror the wire value so non-404 failures (e.g. 410/PartitionKeyRangeGone) surface the same diagnostic signal as a direct ReadItemStreamAsync call.");
        }

        [TestMethod]
        public void BuildSyntheticQueryResponseHeaders_MissingUpstreamSubStatusCode_DefaultsToUnknown()
        {
            // A fresh ResponseMessage has SubStatusCode defaulting to Unknown via the
            // Headers initializer chain; the helper's null-coalesce on SubStatusCode is
            // belt-and-suspenders. Pin the default so a regression that flips it to e.g.
            // BadRequest or Conflict gets caught.
            ResponseMessage pointReadResponse = new ResponseMessage(System.Net.HttpStatusCode.OK);

            CosmosQueryResponseMessageHeaders headers = ReadManyQueryHelper.BuildSyntheticQueryResponseHeaders(
                pointReadResponse: pointReadResponse,
                containerRid: ExpectedContainerRid);

            Assert.AreEqual(SubStatusCodes.Unknown, headers.SubStatusCode, "an upstream response without an explicit SubStatusCode must surface as SubStatusCodes.Unknown on the synthetic header.");
            Assert.AreEqual(0d, headers.RequestCharge, "an upstream response without an explicit RequestCharge must surface as 0 RU on the synthetic header.");
        }

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------

        private static void AssertElementMatchesSampleDocument(CosmosElement element)
        {
            Assert.IsNotNull(element, "expected a parsed CosmosElement from the sample document body.");
            Assert.IsInstanceOfType(element, typeof(CosmosObject), "sample body must round-trip as a JSON object.");

            CosmosObject obj = (CosmosObject)element;
            Assert.IsTrue(
                obj.TryGetValue("_rid", out CosmosString ridString),
                "_rid field must survive the round-trip through the stream helper.");
            Assert.AreEqual(
                SampleDocumentRid,
                ridString.Value,
                "_rid value must round-trip byte-for-byte through the stream helper.");
        }

        /// <summary>
        /// Read-only seekable stream that returns its content in fixed-size chunks
        /// via the synchronous <see cref="Stream.Read(byte[],int,int)"/> contract.
        /// Routes <c>ReadManyQueryHelper.ReadStreamAsCosmosElementAsync</c> down its
        /// <c>CopyToAsync</c> branch (the non-<see cref="MemoryStream"/> path) and
        /// lets tests vary the chunk size to simulate a wire stream that delivers
        /// bytes incrementally.
        /// </summary>
        private sealed class ChunkedReadOnlyStream : Stream
        {
            private readonly byte[] buffer;
            private readonly int chunkSize;
            private long position;

            public ChunkedReadOnlyStream(byte[] buffer, int chunkSize)
            {
                this.buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
                if (chunkSize <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(chunkSize));
                }
                this.chunkSize = chunkSize;
            }

            public override bool CanRead => true;

            public override bool CanSeek => true;

            public override bool CanWrite => false;

            public override long Length => this.buffer.Length;

            public override long Position
            {
                get => this.position;
                set => this.position = value;
            }

            public override int Read(byte[] destination, int offset, int count)
            {
                int remaining = (int)(this.buffer.Length - this.position);
                int available = Math.Min(Math.Min(count, this.chunkSize), remaining);
                if (available <= 0)
                {
                    return 0;
                }

                Array.Copy(this.buffer, this.position, destination, offset, available);
                this.position += available;
                return available;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPosition;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        newPosition = offset;
                        break;
                    case SeekOrigin.Current:
                        newPosition = this.position + offset;
                        break;
                    case SeekOrigin.End:
                        newPosition = this.buffer.Length + offset;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(origin));
                }

                this.position = newPosition;
                return this.position;
            }

            public override void Flush()
            {
                // no-op: stream is read-only and in-memory.
            }

            public override void SetLength(long value) => throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
