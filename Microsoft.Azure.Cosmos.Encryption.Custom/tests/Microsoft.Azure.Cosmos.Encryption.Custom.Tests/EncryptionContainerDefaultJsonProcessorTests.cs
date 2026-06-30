//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.Azure.Cosmos.Encryption.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;
    using TestDoc = Microsoft.Azure.Cosmos.Encryption.Tests.TestCommon.TestDoc;

    /// <summary>
    /// Tests for the container-level default <see cref="JsonProcessor"/> wired through the
    /// <c>WithEncryptor(container, encryptor, defaultJsonProcessor)</c> overload.
    /// </summary>
    [TestClass]
    public class EncryptionContainerDefaultJsonProcessorTests
    {
        private sealed class SimpleDoc
        {
            public string id { get; set; }

            public string pk { get; set; }
        }

        // Offline CosmosClient: the connection string is parsed but no network connection is made
        // until an operation is awaited. Building containers, LINQ queryables and (stream) feed
        // iterators is therefore safe without an emulator.
        private static Container CreateOfflineContainer()
        {
            string dummyKey = Convert.ToBase64String(new byte[48]);
            CosmosClient client = new($"AccountEndpoint=https://localhost:8081/;AccountKey={dummyKey};");
            return client.GetContainer("db", "coll");
        }

        private static Encryptor CreateEncryptor()
        {
            return new Mock<Encryptor>().Object;
        }

        private static JsonProcessor ReadContainerDefault(Container encryptionContainer)
        {
            FieldInfo field = encryptionContainer
                .GetType()
                .GetField("defaultJsonProcessor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Expected EncryptionContainer to expose a private 'defaultJsonProcessor' field.");
            return (JsonProcessor)field.GetValue(encryptionContainer);
        }

        [TestMethod]
        public void WithEncryptor_TwoArg_ReturnsEncryptionContainer_DefaultsToNewtonsoft()
        {
            Container container = CreateOfflineContainer().WithEncryptor(CreateEncryptor());

            Assert.IsInstanceOfType(container, typeof(EncryptionContainer));
            Assert.AreEqual(JsonProcessor.Newtonsoft, ReadContainerDefault(container));
        }

        [TestMethod]
        public void WithEncryptor_ThreeArg_Newtonsoft_SetsContainerDefault()
        {
            Container container = CreateOfflineContainer().WithEncryptor(CreateEncryptor(), JsonProcessor.Newtonsoft);

            Assert.IsInstanceOfType(container, typeof(EncryptionContainer));
            Assert.AreEqual(JsonProcessor.Newtonsoft, ReadContainerDefault(container));
        }

        [TestMethod]
        public void ToEncryptionFeedIterator_Throws_OnNonEncryptionContainer()
        {
            Container container = CreateOfflineContainer();
            IQueryable<SimpleDoc> query = container.GetItemLinqQueryable<SimpleDoc>();

            ArgumentOutOfRangeException ex = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => container.ToEncryptionFeedIterator(query));
            Assert.IsTrue(
                ex.Message.Contains(nameof(EncryptionContainer)),
                $"Expected guard message to mention EncryptionContainer. Actual: {ex.Message}");
        }

        [TestMethod]
        public void ToEncryptionFeedIterator_OnEncryptionContainer_DefaultNewtonsoft_DoesNotThrowGuard()
        {
            Container container = CreateOfflineContainer().WithEncryptor(CreateEncryptor());
            IQueryable<SimpleDoc> query = container.GetItemLinqQueryable<SimpleDoc>();

            FeedIterator<SimpleDoc> iterator = container.ToEncryptionFeedIterator(query);

            Assert.IsNotNull(iterator);
        }

#if NET8_0_OR_GREATER
        [TestMethod]
        public void WithEncryptor_ThreeArg_Stream_SetsContainerDefault()
        {
            Container container = CreateOfflineContainer().WithEncryptor(CreateEncryptor(), JsonProcessor.Stream);

            Assert.IsInstanceOfType(container, typeof(EncryptionContainer));
            Assert.AreEqual(JsonProcessor.Stream, ReadContainerDefault(container));
        }

        // --- White-box threading proof for the LINQ extensions ----------------------------------------
        // ToEncryptionStreamIterator / ToEncryptionFeedIterator must thread the container-level default
        // JsonProcessor into the produced EncryptionFeedIterator. Stream and Newtonsoft decrypt output are
        // byte-identical, so we verify the *selected* processor that was threaded onto the iterator (the seam),
        // not the output. These genuinely fail if the extension drops the container default.
        [TestMethod]
        public void ToEncryptionStreamIterator_DefaultStreamContainer_ThreadsStreamProcessor()
        {
            Container container = CreateOfflineContainer().WithEncryptor(CreateEncryptor(), JsonProcessor.Stream);
            IQueryable<SimpleDoc> query = container.GetItemLinqQueryable<SimpleDoc>();

            FeedIterator iterator = container.ToEncryptionStreamIterator(query);

            Assert.IsNotNull(iterator);
            Assert.AreEqual(JsonProcessor.Stream, ReadIteratorDefaultProcessor(iterator));
        }

        [TestMethod]
        public void ToEncryptionFeedIterator_DefaultStreamContainer_ThreadsStreamProcessor()
        {
            Container container = CreateOfflineContainer().WithEncryptor(CreateEncryptor(), JsonProcessor.Stream);
            IQueryable<SimpleDoc> query = container.GetItemLinqQueryable<SimpleDoc>();

            FeedIterator<SimpleDoc> iterator = container.ToEncryptionFeedIterator(query);

            Assert.IsNotNull(iterator);
            Assert.AreEqual(JsonProcessor.Stream, ReadIteratorDefaultProcessor(iterator));
        }

        [TestMethod]
        public void ToEncryptionStreamIterator_DefaultNewtonsoftContainer_ThreadsNewtonsoftProcessor()
        {
            Container container = CreateOfflineContainer().WithEncryptor(CreateEncryptor(), JsonProcessor.Newtonsoft);
            IQueryable<SimpleDoc> query = container.GetItemLinqQueryable<SimpleDoc>();

            FeedIterator iterator = container.ToEncryptionStreamIterator(query);

            Assert.AreEqual(JsonProcessor.Newtonsoft, ReadIteratorDefaultProcessor(iterator));
        }

        // --- Genuine end-to-end decrypt round-trip on the feed/query path ------------------------------
        // A default-Stream container reads an encrypted feed via GetItemQueryStreamIterator and must
        // (a) decrypt the data correctly and (b) select the Stream processor on the decrypt path. The
        // white-box proof is the 'EncryptionProcessor.Decrypt.Mde.Stream' diagnostics scope captured from
        // the encryption ActivitySource (decrypt output is byte-identical to Newtonsoft, so the scope is the
        // only observable difference). This fails if the default is not threaded (the legacy JObject path
        // emits no Mde selection scope at all).
        [TestMethod]
        public async Task GetItemQueryStreamIterator_DefaultStreamContainer_DecryptsAndSelectsStream()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            byte[] feedBytes = await BuildEncryptedFeedAsync(encryptor, doc);
            EncryptionContainer container = BuildMockEncryptionContainer(encryptor, JsonProcessor.Stream, feedBytes);

            List<string> scopes = new();
            using (RegisterDecryptScopeListener(scopes))
            {
                FeedIterator iterator = container.GetItemQueryStreamIterator(new QueryDefinition("SELECT * FROM c"));
                using ResponseMessage response = await iterator.ReadNextAsync();

                Assert.IsTrue(response.IsSuccessStatusCode);
                AssertSingleDocumentRoundTrips(response, doc);
            }

            CollectionAssert.Contains(
                scopes,
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream,
                $"Expected the Stream decrypt-selection scope on the feed path. Captured: {string.Join(", ", scopes)}");
        }

        [TestMethod]
        public async Task GetItemQueryIterator_DefaultStreamContainer_DecryptsAndSelectsStream()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            byte[] feedBytes = await BuildEncryptedFeedAsync(encryptor, doc);
            EncryptionContainer container = BuildMockEncryptionContainer(encryptor, JsonProcessor.Stream, feedBytes);

            List<string> scopes = new();
            FeedResponse<TestDoc> response;
            using (RegisterDecryptScopeListener(scopes))
            {
                FeedIterator<TestDoc> iterator = container.GetItemQueryIterator<TestDoc>(new QueryDefinition("SELECT * FROM c"));
                response = await iterator.ReadNextAsync();
            }

            TestDoc decrypted = response.Resource.Single();
            Assert.AreEqual(doc.SensitiveStr, decrypted.SensitiveStr);
            Assert.AreEqual(doc, decrypted);
            CollectionAssert.Contains(
                scopes,
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream,
                $"Expected the Stream decrypt-selection scope on the typed query path. Captured: {string.Join(", ", scopes)}");
        }

        // Contrast: a Newtonsoft-default container decrypts correctly but never selects Stream on the feed path.
        [TestMethod]
        public async Task GetItemQueryStreamIterator_DefaultNewtonsoftContainer_DecryptsWithoutStreamSelection()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            byte[] feedBytes = await BuildEncryptedFeedAsync(encryptor, doc);
            EncryptionContainer container = BuildMockEncryptionContainer(encryptor, JsonProcessor.Newtonsoft, feedBytes);

            List<string> scopes = new();
            using (RegisterDecryptScopeListener(scopes))
            {
                FeedIterator iterator = container.GetItemQueryStreamIterator(new QueryDefinition("SELECT * FROM c"));
                using ResponseMessage response = await iterator.ReadNextAsync();

                AssertSingleDocumentRoundTrips(response, doc);
            }

            CollectionAssert.DoesNotContain(
                scopes,
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream,
                $"Newtonsoft-default feed path must not select Stream. Captured: {string.Join(", ", scopes)}");
        }

        // Per-request override is honored on the feed path: a Newtonsoft-default container with a per-request
        // Stream override must select Stream. This fails if requestOptions is not threaded into the feed iterator.
        [TestMethod]
        public async Task GetItemQueryStreamIterator_NewtonsoftDefault_StreamPerRequestOverride_SelectsStream()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            byte[] feedBytes = await BuildEncryptedFeedAsync(encryptor, doc);
            EncryptionContainer container = BuildMockEncryptionContainer(encryptor, JsonProcessor.Newtonsoft, feedBytes);

            QueryRequestOptions requestOptions = new()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Stream },
                },
            };

            List<string> scopes = new();
            using (RegisterDecryptScopeListener(scopes))
            {
                FeedIterator iterator = container.GetItemQueryStreamIterator(new QueryDefinition("SELECT * FROM c"), requestOptions: requestOptions);
                using ResponseMessage response = await iterator.ReadNextAsync();

                AssertSingleDocumentRoundTrips(response, doc);
            }

            CollectionAssert.Contains(
                scopes,
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream,
                $"Per-request Stream override must select Stream on the feed path. Captured: {string.Join(", ", scopes)}");
        }

        // Per-request override wins over a Stream container default: a Newtonsoft override downgrades selection.
        [TestMethod]
        public async Task GetItemQueryStreamIterator_StreamDefault_NewtonsoftPerRequestOverride_OverrideWins()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            byte[] feedBytes = await BuildEncryptedFeedAsync(encryptor, doc);
            EncryptionContainer container = BuildMockEncryptionContainer(encryptor, JsonProcessor.Stream, feedBytes);

            QueryRequestOptions requestOptions = new()
            {
                Properties = new Dictionary<string, object>
                {
                    { JsonProcessorRequestOptionsExtensions.JsonProcessorPropertyBagKey, JsonProcessor.Newtonsoft },
                },
            };

            List<string> scopes = new();
            using (RegisterDecryptScopeListener(scopes))
            {
                FeedIterator iterator = container.GetItemQueryStreamIterator(new QueryDefinition("SELECT * FROM c"), requestOptions: requestOptions);
                using ResponseMessage response = await iterator.ReadNextAsync();

                AssertSingleDocumentRoundTrips(response, doc);
            }

            CollectionAssert.DoesNotContain(
                scopes,
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream,
                $"Per-request Newtonsoft override must win over the Stream default. Captured: {string.Join(", ", scopes)}");
        }

        // --- Genuine end-to-end decrypt round-trip on the POINT-READ path -------------------------------
        // A default-Stream container reads a single encrypted item via ReadItemStreamAsync and must
        // (a) decrypt the document correctly and (b) select the Stream processor on the decrypt path. As on the
        // feed path, Stream and Newtonsoft decrypt output are byte-identical, so the observable proof is the
        // 'EncryptionProcessor.Decrypt.Mde.Stream' diagnostics scope captured from the encryption ActivitySource.
        // This fails (no Stream scope) if the container default is not threaded into ReadItemHelperAsync's
        // DecryptAsync call - the same seam the feed/query/change-feed tests cover, here on the point-read path.
        [TestMethod]
        public async Task ReadItemStreamAsync_DefaultStreamContainer_DecryptsAndSelectsStream()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            byte[] documentBytes = await BuildEncryptedItemBytesAsync(encryptor, doc);
            EncryptionContainer container = BuildMockPointReadContainer(encryptor, JsonProcessor.Stream, documentBytes);

            List<string> scopes = new();
            using (RegisterDecryptScopeListener(scopes))
            {
                using ResponseMessage response = await container.ReadItemStreamAsync(doc.Id, new PartitionKey(doc.PK));

                Assert.IsTrue(response.IsSuccessStatusCode);
                AssertSingleItemRoundTrips(response, doc);
            }

            CollectionAssert.Contains(
                scopes,
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream,
                $"Expected the Stream decrypt-selection scope on the point-read path. Captured: {string.Join(", ", scopes)}");
        }

        // Contrast: a Newtonsoft-default container decrypts the same item correctly but never selects Stream on
        // the point-read path. Proves the Stream scope is a genuine discriminator here, not always-on.
        [TestMethod]
        public async Task ReadItemStreamAsync_DefaultNewtonsoftContainer_DecryptsWithoutStreamSelection()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            byte[] documentBytes = await BuildEncryptedItemBytesAsync(encryptor, doc);
            EncryptionContainer container = BuildMockPointReadContainer(encryptor, JsonProcessor.Newtonsoft, documentBytes);

            List<string> scopes = new();
            using (RegisterDecryptScopeListener(scopes))
            {
                using ResponseMessage response = await container.ReadItemStreamAsync(doc.Id, new PartitionKey(doc.PK));

                AssertSingleItemRoundTrips(response, doc);
            }

            CollectionAssert.DoesNotContain(
                scopes,
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream,
                $"Newtonsoft-default point-read path must not select Stream. Captured: {string.Join(", ", scopes)}");
        }

        // --- Genuine change-feed-processor path (BLOCKING 1) -------------------------------------------
        // The typed GetChangeFeedProcessorBuilder<T> handler surfaces documents as JObject and funnels them
        // through DecryptChangeFeedDocumentsAsync<T>. A default-Stream container must decrypt via the Stream
        // processor (emitting 'EncryptionProcessor.Decrypt.Mde.Stream') instead of silently dropping to the
        // Newtonsoft (JObject) decryptor. We capture the SDK wrapper delegate the EncryptionContainer registers
        // on the inner container, invoke it with encrypted JObjects (exactly as the change-feed engine would),
        // and assert both the decrypted round-trip and the per-document Stream decrypt-selection scope. This
        // fails (no Stream scope) if the container default is not threaded into the change-feed decrypt call.
        [TestMethod]
        public async Task ChangeFeedProcessor_DefaultStreamContainer_DecryptsAndSelectsStream()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc1 = TestDoc.Create();
            TestDoc doc2 = TestDoc.Create();
            JObject encrypted1 = await BuildEncryptedDocumentAsync(encryptor, doc1);
            JObject encrypted2 = await BuildEncryptedDocumentAsync(encryptor, doc2);

            List<TestDoc> received = new();
            Container.ChangesHandler<TestDoc> typedHandler = (changes, _) =>
            {
                received.AddRange(changes);
                return Task.CompletedTask;
            };

            Container.ChangesHandler<JObject> wrapper = CaptureChangeFeedWrapper(encryptor, JsonProcessor.Stream, typedHandler);

            List<string> scopes = new();
            using (RegisterDecryptScopeListener(scopes))
            {
                await wrapper(new List<JObject> { encrypted1, encrypted2 }, CancellationToken.None);
            }

            Assert.AreEqual(2, received.Count, "Expected both change-feed documents to be surfaced to the typed handler.");
            Assert.AreEqual(doc1, received[0]);
            Assert.AreEqual(doc1.SensitiveStr, received[0].SensitiveStr);
            Assert.AreEqual(doc2, received[1]);
            Assert.AreEqual(doc2.SensitiveStr, received[1].SensitiveStr);

            int streamSelections = scopes.Count(s => s == CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream);
            Assert.AreEqual(
                2,
                streamSelections,
                $"Expected one Stream decrypt-selection scope per change-feed document. Captured: {string.Join(", ", scopes)}");
        }

        // Contrast: a Newtonsoft-default container decrypts the same change-feed documents correctly but never
        // selects Stream. Proves the Stream scope is a genuine discriminator on this path, not always-on.
        [TestMethod]
        public async Task ChangeFeedProcessor_DefaultNewtonsoftContainer_DecryptsWithoutStreamSelection()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc1 = TestDoc.Create();
            TestDoc doc2 = TestDoc.Create();
            JObject encrypted1 = await BuildEncryptedDocumentAsync(encryptor, doc1);
            JObject encrypted2 = await BuildEncryptedDocumentAsync(encryptor, doc2);

            List<TestDoc> received = new();
            Container.ChangesHandler<TestDoc> typedHandler = (changes, _) =>
            {
                received.AddRange(changes);
                return Task.CompletedTask;
            };

            Container.ChangesHandler<JObject> wrapper = CaptureChangeFeedWrapper(encryptor, JsonProcessor.Newtonsoft, typedHandler);

            List<string> scopes = new();
            using (RegisterDecryptScopeListener(scopes))
            {
                await wrapper(new List<JObject> { encrypted1, encrypted2 }, CancellationToken.None);
            }

            Assert.AreEqual(2, received.Count, "Expected both change-feed documents to be surfaced to the typed handler.");
            Assert.AreEqual(doc1, received[0]);
            Assert.AreEqual(doc2, received[1]);
            CollectionAssert.DoesNotContain(
                scopes,
                CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream,
                $"Newtonsoft-default change-feed path must not select Stream. Captured: {string.Join(", ", scopes)}");
        }

        // --- Lazy DecryptableItem honors the container default (A1) ------------------------------------
        // The lazy read paths (point-read and query/feed iterator) surface an undecrypted DecryptableItem whose
        // GetItemAsync<T>() performs the actual decryption on demand. Before A1 that decryption was hard-wired to the
        // Newtonsoft (JObject) decryptor and silently ignored a container default of JsonProcessor.Stream. These tests
        // drive the production lazy paths end-to-end over mocked encrypted responses (no emulator) and assert both the
        // correct decrypted round-trip AND that the configured processor was actually used (Stream emits the
        // 'EncryptionProcessor.Decrypt.Mde.Stream' selection scope; Newtonsoft never does).

        [DataTestMethod]
        [DataRow(JsonProcessor.Newtonsoft)]
        [DataRow(JsonProcessor.Stream)]
        public async Task ReadItemAsync_DecryptableItem_DefaultProcessor_DecryptsAndHonorsProcessor(JsonProcessor processor)
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            byte[] documentBytes = await BuildEncryptedItemBytesAsync(encryptor, doc);
            EncryptionContainer container = BuildMockPointReadContainer(encryptor, processor, documentBytes);

            ItemResponse<DecryptableItem> response = await container.ReadItemAsync<DecryptableItem>(doc.Id, new PartitionKey(doc.PK));
            Assert.AreEqual(
                processor,
                ReadDecryptableItemProcessor(response.Resource),
                "Point-read DecryptableItem must carry the container default processor.");

            await AssertLazyItemDecryptsWithProcessor(response.Resource, doc, processor);
        }

        [DataTestMethod]
        [DataRow(JsonProcessor.Newtonsoft)]
        [DataRow(JsonProcessor.Stream)]
        public async Task GetItemQueryIterator_DecryptableItem_DefaultProcessor_DecryptsAndHonorsProcessor(JsonProcessor processor)
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            byte[] feedBytes = await BuildEncryptedFeedAsync(encryptor, doc);
            EncryptionContainer container = BuildMockEncryptionContainer(encryptor, processor, feedBytes);

            FeedIterator<DecryptableItem> iterator = container.GetItemQueryIterator<DecryptableItem>(new QueryDefinition("SELECT * FROM c"));
            Assert.IsTrue(iterator.HasMoreResults);
            FeedResponse<DecryptableItem> page = await iterator.ReadNextAsync();
            DecryptableItem item = page.Single();
            Assert.AreEqual(
                processor,
                ReadDecryptableItemProcessor(item),
                "Feed/query DecryptableItem must carry the container default processor.");

            await AssertLazyItemDecryptsWithProcessor(item, doc, processor);
        }

        // Guardrail (release blocker if it fails): the SAME encrypted document decrypted lazily via Newtonsoft and via
        // Stream must yield byte-for-byte identical plaintext and an equivalent DecryptionContext. Proves A1 honors the
        // processor choice without changing the decrypted result on either path.
        [TestMethod]
        public async Task DecryptableItem_NewtonsoftAndStream_ProduceIdenticalPlaintextAndContext()
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            JObject encrypted = await BuildEncryptedDocumentAsync(encryptor, doc);

            DecryptableItemCore newtonsoftItem = new((JObject)encrypted.DeepClone(), encryptor.Object, TestSerializer, JsonProcessor.Newtonsoft);
            DecryptableItemCore streamItem = new((JObject)encrypted.DeepClone(), encryptor.Object, TestSerializer, JsonProcessor.Stream);

            (JObject plaintextNewtonsoft, DecryptionContext contextNewtonsoft) = await newtonsoftItem.GetItemAsync<JObject>();
            (JObject plaintextStream, DecryptionContext contextStream) = await streamItem.GetItemAsync<JObject>();

            byte[] bytesNewtonsoft = ReadAllBytes(EncryptionProcessor.BaseSerializer.ToStream(plaintextNewtonsoft));
            byte[] bytesStream = ReadAllBytes(EncryptionProcessor.BaseSerializer.ToStream(plaintextStream));
            CollectionAssert.AreEqual(
                bytesNewtonsoft,
                bytesStream,
                "Stream and Newtonsoft lazy decryption must produce byte-for-byte identical plaintext.");

            // Both must also faithfully reconstruct the original document.
            Assert.AreEqual(doc, plaintextNewtonsoft.ToObject<TestDoc>());
            Assert.AreEqual(doc, plaintextStream.ToObject<TestDoc>());

            AssertDecryptionContextEquivalent(contextNewtonsoft, contextStream);
        }

        // Write-path wrappers: CreateItem/ReplaceItem/UpsertItem build an EncryptableItem<T>/EncryptableItemStream and
        // call SetDecryptableItem(...) to expose lazy decryption of the server echo. A1 threads the container default
        // through that signature; these tests prove the produced DecryptableItem carries the processor and decrypts
        // with it.
        [DataTestMethod]
        [DataRow(JsonProcessor.Newtonsoft)]
        [DataRow(JsonProcessor.Stream)]
        public async Task SetDecryptableItem_EncryptableItemT_ThreadsProcessorIntoLazyDecrypt(JsonProcessor processor)
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            JObject encrypted = await BuildEncryptedDocumentAsync(encryptor, doc);

            EncryptableItem<TestDoc> encryptableItem = new(doc);
            encryptableItem.SetDecryptableItem((JObject)encrypted.DeepClone(), encryptor.Object, TestSerializer, processor);

            Assert.AreEqual(processor, ReadDecryptableItemProcessor(encryptableItem.DecryptableItem));
            await AssertLazyItemDecryptsWithProcessor(encryptableItem.DecryptableItem, doc, processor);
        }

        [DataTestMethod]
        [DataRow(JsonProcessor.Newtonsoft)]
        [DataRow(JsonProcessor.Stream)]
        public async Task SetDecryptableItem_EncryptableItemStream_ThreadsProcessorIntoLazyDecrypt(JsonProcessor processor)
        {
            Mock<Encryptor> encryptor = CreateMdeEncryptor();
            TestDoc doc = TestDoc.Create();
            JObject encrypted = await BuildEncryptedDocumentAsync(encryptor, doc);

            using EncryptableItemStream encryptableItem = new(doc.ToStream());
            encryptableItem.SetDecryptableItem((JObject)encrypted.DeepClone(), encryptor.Object, TestSerializer, processor);

            Assert.AreEqual(processor, ReadDecryptableItemProcessor(encryptableItem.DecryptableItem));
            await AssertLazyItemDecryptsWithProcessor(encryptableItem.DecryptableItem, doc, processor);
        }

        // --- Helpers for the genuine round-trip tests --------------------------------------------------
        private const string DekId = "dekId";

        private static Mock<Encryptor> CreateMdeEncryptor()
        {
            return TestEncryptorFactory.CreateMde(DekId, out _);
        }

        private static async Task<JObject> BuildEncryptedDocumentAsync(Mock<Encryptor> encryptor, TestDoc doc)
        {
            EncryptionOptions options = new()
            {
                DataEncryptionKeyId = DekId,
#pragma warning disable CS0618 // Type or member is obsolete
                EncryptionAlgorithm = CosmosEncryptionAlgorithm.MdeAeadAes256CbcHmac256Randomized,
#pragma warning restore CS0618 // Type or member is obsolete
                PathsToEncrypt = TestDoc.PathsToEncrypt,
            };

            CosmosDiagnosticsContext diagnosticsContext = CosmosDiagnosticsContext.Create(null);

            // Encrypt with Newtonsoft (canonical MDE ciphertext). The MDE _ei payload is processor-agnostic, so
            // the Stream decryptor under test must round-trip it identically; this also exercises cross-processor interop.
            Stream encryptedStream = await EncryptionProcessor.EncryptAsync(
                doc.ToStream(),
                encryptor.Object,
                options,
                JsonProcessor.Newtonsoft,
                diagnosticsContext,
                CancellationToken.None);

            JObject encryptedDocument = EncryptionProcessor.BaseSerializer.FromStream<JObject>(encryptedStream);
            Assert.IsNotNull(encryptedDocument.Property(Constants.EncryptedInfo), "Test setup expected the document to be encrypted (_ei present).");
            return encryptedDocument;
        }

        private static async Task<byte[]> BuildEncryptedFeedAsync(Mock<Encryptor> encryptor, TestDoc doc)
        {
            JObject encryptedDocument = await BuildEncryptedDocumentAsync(encryptor, doc);

            JObject feed = new()
            {
                ["_rid"] = "rid",
                ["Documents"] = new JArray(encryptedDocument),
                ["_count"] = 1,
            };

            return Encoding.UTF8.GetBytes(feed.ToString());
        }

        private static EncryptionContainer BuildMockEncryptionContainer(
            Mock<Encryptor> encryptor,
            JsonProcessor defaultJsonProcessor,
            byte[] feedBytes)
        {
            string dummyKey = Convert.ToBase64String(new byte[48]);
            CosmosClient client = new($"AccountEndpoint=https://localhost:8081/;AccountKey={dummyKey};");
            Database database = client.GetDatabase("db");

            Mock<FeedIterator> feedIterator = new();
            bool consumed = false;
            feedIterator.SetupGet(f => f.HasMoreResults).Returns(() => !consumed);
            feedIterator
                .Setup(f => f.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    consumed = true;
                    return new ResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new MemoryStream(feedBytes),
                    };
                });

            Mock<Container> innerContainer = new();
            innerContainer.SetupGet(c => c.Database).Returns(database);
            innerContainer
                .Setup(c => c.GetItemQueryStreamIterator(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
                .Returns(feedIterator.Object);

            return new EncryptionContainer(innerContainer.Object, encryptor.Object, defaultJsonProcessor);
        }

        private static async Task<byte[]> BuildEncryptedItemBytesAsync(Mock<Encryptor> encryptor, TestDoc doc)
        {
            JObject encryptedDocument = await BuildEncryptedDocumentAsync(encryptor, doc);
            return Encoding.UTF8.GetBytes(encryptedDocument.ToString());
        }

        // Point-read counterpart of BuildMockEncryptionContainer: mocks the inner container's ReadItemStreamAsync
        // (the exact call EncryptionContainer.ReadItemHelperAsync makes) to return a single encrypted item stream,
        // so container.ReadItemStreamAsync runs the production point-read decrypt path with no emulator.
        private static EncryptionContainer BuildMockPointReadContainer(
            Mock<Encryptor> encryptor,
            JsonProcessor defaultJsonProcessor,
            byte[] documentBytes)
        {
            string dummyKey = Convert.ToBase64String(new byte[48]);
            CosmosClient client = new($"AccountEndpoint=https://localhost:8081/;AccountKey={dummyKey};");
            Database database = client.GetDatabase("db");

            Mock<Container> innerContainer = new();
            innerContainer.SetupGet(c => c.Database).Returns(database);
            innerContainer
                .Setup(c => c.ReadItemStreamAsync(
                    It.IsAny<string>(),
                    It.IsAny<PartitionKey>(),
                    It.IsAny<ItemRequestOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new MemoryStream(documentBytes),
                });

            return new EncryptionContainer(innerContainer.Object, encryptor.Object, defaultJsonProcessor);
        }
        // registers on the inner container. The captured wrapper, when invoked with JObject documents, runs the
        // exact production decrypt path (DecryptChangeFeedDocumentsAsync<T>) and forwards decrypted items to the
        // supplied typed handler — no emulator required.
        private static Container.ChangesHandler<JObject> CaptureChangeFeedWrapper(
            Mock<Encryptor> encryptor,
            JsonProcessor defaultJsonProcessor,
            Container.ChangesHandler<TestDoc> typedHandler)
        {
            string dummyKey = Convert.ToBase64String(new byte[48]);
            CosmosClient client = new($"AccountEndpoint=https://localhost:8081/;AccountKey={dummyKey};");
            Database database = client.GetDatabase("db");

            Container.ChangesHandler<JObject> capturedWrapper = null;
            Mock<Container> innerContainer = new();
            innerContainer.SetupGet(c => c.Database).Returns(database);
            innerContainer
                .Setup(c => c.GetChangeFeedProcessorBuilder(
                    It.IsAny<string>(),
                    It.IsAny<Container.ChangesHandler<JObject>>()))
                .Callback<string, Container.ChangesHandler<JObject>>((_, wrapper) => capturedWrapper = wrapper)
                .Returns((ChangeFeedProcessorBuilder)null);

            EncryptionContainer encryptionContainer = new(innerContainer.Object, encryptor.Object, defaultJsonProcessor);
            encryptionContainer.GetChangeFeedProcessorBuilder("proc", typedHandler);

            Assert.IsNotNull(capturedWrapper, "EncryptionContainer did not register a change-feed wrapper on the inner container.");
            return capturedWrapper;
        }

        private static ActivityListener RegisterDecryptScopeListener(List<string> capturedDisplayNames)
        {
            ActivityListener listener = new()
            {
                ShouldListenTo = source => source.Name == "Microsoft.Azure.Cosmos.Encryption.Custom",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStarted = activity =>
                {
                    lock (capturedDisplayNames)
                    {
                        capturedDisplayNames.Add(activity.DisplayName);
                    }
                },
            };

            ActivitySource.AddActivityListener(listener);
            return listener;
        }

        private static void AssertSingleDocumentRoundTrips(ResponseMessage response, TestDoc expected)
        {
            JObject feed = EncryptionProcessor.BaseSerializer.FromStream<JObject>(response.Content);
            JObject decrypted = (JObject)((JArray)feed["Documents"]).Single();
            Assert.AreEqual(expected.SensitiveStr, decrypted.Value<string>(nameof(TestDoc.SensitiveStr)));
            Assert.AreEqual(expected.SensitiveInt, decrypted.Value<int>(nameof(TestDoc.SensitiveInt)));
            Assert.IsNull(decrypted.Property(Constants.EncryptedInfo), "Decrypted document must not carry the _ei property.");
        }

        // Point-read counterpart of AssertSingleDocumentRoundTrips: the response Content is the decrypted item
        // itself (no feed envelope). Asserts the encrypted fields round-trip and the _ei property was removed.
        private static void AssertSingleItemRoundTrips(ResponseMessage response, TestDoc expected)
        {
            JObject decrypted = EncryptionProcessor.BaseSerializer.FromStream<JObject>(response.Content);
            Assert.AreEqual(expected.SensitiveStr, decrypted.Value<string>(nameof(TestDoc.SensitiveStr)));
            Assert.AreEqual(expected.SensitiveInt, decrypted.Value<int>(nameof(TestDoc.SensitiveInt)));
            Assert.IsNull(decrypted.Property(Constants.EncryptedInfo), "Decrypted item must not carry the _ei property.");
        }

        private static JsonProcessor ReadIteratorDefaultProcessor(object iterator)
        {
            object target = iterator;
            if (target.GetType().GetField("defaultJsonProcessor", BindingFlags.NonPublic | BindingFlags.Instance) == null)
            {
                // Generic EncryptionFeedIterator<T> wraps an inner non-generic EncryptionFeedIterator.
                FieldInfo innerField = target.GetType().GetField("feedIterator", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNotNull(innerField, "Expected a wrapped 'feedIterator' field on EncryptionFeedIterator<T>.");
                target = innerField.GetValue(iterator);
                Assert.IsNotNull(target, "Wrapped feedIterator was null.");
            }

            FieldInfo field = target.GetType().GetField("defaultJsonProcessor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Expected EncryptionFeedIterator to expose a private 'defaultJsonProcessor' field.");
            return (JsonProcessor)field.GetValue(target);
        }

        private static async Task AssertLazyItemDecryptsWithProcessor(DecryptableItem item, TestDoc expected, JsonProcessor processor)
        {
            List<string> scopes = new();
            TestDoc decrypted;
            DecryptionContext decryptionContext;
            using (RegisterDecryptScopeListener(scopes))
            {
                (decrypted, decryptionContext) = await item.GetItemAsync<TestDoc>();
            }

            Assert.AreEqual(expected, decrypted);
            Assert.AreEqual(expected.SensitiveStr, decrypted.SensitiveStr);
            AssertProcessorSelection(processor, scopes);
            AssertDecryptedDekId(decryptionContext);
        }

        private static void AssertProcessorSelection(JsonProcessor processor, List<string> scopes)
        {
            string streamScope = CosmosDiagnosticsContext.ScopeDecryptModeSelectionPrefix + JsonProcessor.Stream;
            if (processor == JsonProcessor.Stream)
            {
                CollectionAssert.Contains(
                    scopes,
                    streamScope,
                    $"Lazy decryption under the Stream default must select the Stream processor. Captured: {string.Join(", ", scopes)}");
            }
            else
            {
                CollectionAssert.DoesNotContain(
                    scopes,
                    streamScope,
                    $"Lazy decryption under the Newtonsoft default must not select the Stream processor. Captured: {string.Join(", ", scopes)}");
            }
        }

        private static void AssertDecryptedDekId(DecryptionContext decryptionContext)
        {
            Assert.IsNotNull(decryptionContext);
            Assert.IsTrue(decryptionContext.DecryptionInfoList.Count > 0, "Expected at least one DecryptionInfo entry.");
            foreach (DecryptionInfo info in decryptionContext.DecryptionInfoList)
            {
                Assert.AreEqual(DekId, info.DataEncryptionKeyId);
                Assert.IsTrue(info.PathsDecrypted.Count > 0, "Expected the decrypted paths to be reported.");
            }
        }

        private static void AssertDecryptionContextEquivalent(DecryptionContext expected, DecryptionContext actual)
        {
            Assert.IsNotNull(expected);
            Assert.IsNotNull(actual);
            Assert.AreEqual(
                expected.DecryptionInfoList.Count,
                actual.DecryptionInfoList.Count,
                "DecryptionContext entry count differs between processors.");
            for (int i = 0; i < expected.DecryptionInfoList.Count; i++)
            {
                Assert.AreEqual(
                    expected.DecryptionInfoList[i].DataEncryptionKeyId,
                    actual.DecryptionInfoList[i].DataEncryptionKeyId,
                    "DataEncryptionKeyId differs between processors.");
                CollectionAssert.AreEquivalent(
                    expected.DecryptionInfoList[i].PathsDecrypted.ToList(),
                    actual.DecryptionInfoList[i].PathsDecrypted.ToList(),
                    "Decrypted paths differ between processors.");
            }
        }

        private static JsonProcessor ReadDecryptableItemProcessor(DecryptableItem item)
        {
            FieldInfo field = item.GetType().GetField("jsonProcessor", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "Expected DecryptableItemCore to expose a private 'jsonProcessor' field.");
            return (JsonProcessor)field.GetValue(item);
        }

        private static readonly CosmosSerializer TestSerializer = new JObjectCosmosSerializer();

        private static byte[] ReadAllBytes(Stream stream)
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            using MemoryStream memoryStream = new();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }

        // Minimal CosmosSerializer for directly constructing DecryptableItemCore / driving SetDecryptableItem in
        // tests. The production point-read/feed paths supply the container's CosmosSerializer; here we wrap the same
        // JSON.NET serializer the package uses internally so deserialization matches production exactly.
        private sealed class JObjectCosmosSerializer : CosmosSerializer
        {
            public override T FromStream<T>(Stream stream)
            {
                return EncryptionProcessor.BaseSerializer.FromStream<T>(stream);
            }

            public override Stream ToStream<T>(T input)
            {
                return EncryptionProcessor.BaseSerializer.ToStream(input);
            }
        }
#endif
    }
}
