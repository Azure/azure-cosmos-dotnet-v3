//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Encryption.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class PdekCacheRefreshWorkerTests
    {
        [TestInitialize]
        public void TestSetup()
        {
            // Reset the static TTL to the default before each test to avoid cross-test interference.
            ProtectedDataEncryptionKey.TimeToLive = TimeSpan.FromHours(2);
        }

        [TestMethod]
        public void WorkerNotCreatedWhenTtlBelowOneHour()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver> mockResolver =
                new Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver>();

            EncryptionCosmosClient encryptionClient = new EncryptionCosmosClient(
                mockClient.Object,
                mockResolver.Object,
                "AZURE_KEY_VAULT",
                TimeSpan.FromMinutes(30));

            Assert.IsNull(encryptionClient.CacheRefreshWorker);
            encryptionClient.Dispose();
        }

        [TestMethod]
        public void WorkerNotCreatedWhenTtlIsZero()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver> mockResolver =
                new Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver>();

            EncryptionCosmosClient encryptionClient = new EncryptionCosmosClient(
                mockClient.Object,
                mockResolver.Object,
                "AZURE_KEY_VAULT",
                TimeSpan.Zero);

            Assert.IsNull(encryptionClient.CacheRefreshWorker);
            encryptionClient.Dispose();
        }

        [TestMethod]
        public void WorkerCreatedWhenTtlIsOneHour()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver> mockResolver =
                new Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver>();

            EncryptionCosmosClient encryptionClient = new EncryptionCosmosClient(
                mockClient.Object,
                mockResolver.Object,
                "AZURE_KEY_VAULT",
                TimeSpan.FromHours(1));

            Assert.IsNotNull(encryptionClient.CacheRefreshWorker);
            encryptionClient.Dispose();
        }

        [TestMethod]
        public void WorkerCreatedWhenTtlExceedsOneHour()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver> mockResolver =
                new Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver>();

            EncryptionCosmosClient encryptionClient = new EncryptionCosmosClient(
                mockClient.Object,
                mockResolver.Object,
                "AZURE_KEY_VAULT",
                TimeSpan.FromHours(2));

            Assert.IsNotNull(encryptionClient.CacheRefreshWorker);
            encryptionClient.Dispose();
        }

        [TestMethod]
        public void WorkerCreatedWithDefaultTtl()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver> mockResolver =
                new Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver>();

            // Default TTL is 1 hour (set in constructor when null)
            EncryptionCosmosClient encryptionClient = new EncryptionCosmosClient(
                mockClient.Object,
                mockResolver.Object,
                "AZURE_KEY_VAULT",
                keyCacheTimeToLive: null);

            Assert.IsNotNull(encryptionClient.CacheRefreshWorker);
            encryptionClient.Dispose();
        }

        [TestMethod]
        public async Task DisposeCancelsWorkerLoop()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver> mockResolver =
                new Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver>();

            EncryptionCosmosClient encryptionClient = new EncryptionCosmosClient(
                mockClient.Object,
                mockResolver.Object,
                "AZURE_KEY_VAULT",
                TimeSpan.FromHours(1));

            PdekCacheRefreshWorker worker = encryptionClient.CacheRefreshWorker;
            Assert.IsNotNull(worker);

            encryptionClient.Dispose();

            // The worker task should complete shortly after disposal.
            await Task.WhenAny(worker.WorkerTask, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.IsTrue(worker.WorkerTask.IsCompleted);
        }

        [TestMethod]
        public void TrackEntryAfterDisposalIsNoOp()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver> mockResolver =
                new Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver>();

            EncryptionCosmosClient encryptionClient = new EncryptionCosmosClient(
                mockClient.Object,
                mockResolver.Object,
                "AZURE_KEY_VAULT",
                TimeSpan.FromHours(1));

            PdekCacheRefreshWorker worker = encryptionClient.CacheRefreshWorker;
            encryptionClient.Dispose();

            // Should not throw after disposal
            worker.TrackEntry("key1", null, "dbRid1");
        }

        [TestMethod]
        public void TrackEntryRegistersEntry()
        {
            Mock<CosmosClient> mockClient = new Mock<CosmosClient>();
            Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver> mockResolver =
                new Mock<global::Azure.Core.Cryptography.IKeyEncryptionKeyResolver>();

            EncryptionCosmosClient encryptionClient = new EncryptionCosmosClient(
                mockClient.Object,
                mockResolver.Object,
                "AZURE_KEY_VAULT",
                TimeSpan.FromHours(1));

            PdekCacheRefreshWorker worker = encryptionClient.CacheRefreshWorker;
            Assert.IsNotNull(worker);

            // Track entries with null container (worker only stores the reference, doesn't use it during track)
            worker.TrackEntry("cek1", null, "dbRid1");
            worker.TrackEntry("cek2", null, "dbRid1");

            // Track the same entry again (update path) — should not throw
            worker.TrackEntry("cek1", null, "dbRid1");

            encryptionClient.Dispose();
        }
    }
}
