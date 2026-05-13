//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Caching.Distributed;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    /// <summary>
    /// Locks in the IDisposable / IAsyncDisposable contract added to <see cref="DekCache"/> and
    /// <see cref="CosmosDataEncryptionKeyProvider"/>: cancel + bounded drain of in-flight L2
    /// writes, idempotent disposal, ObjectDisposedException after disposal, and no
    /// UnobservedTaskException on faulted background writes.
    /// </summary>
    [TestClass]
    public class DekCacheLifecycleTests
    {
        private static DataEncryptionKeyProperties NewDek(string id, string wrappedSuffix = "v1")
        {
            return new DataEncryptionKeyProperties(
                id,
                "AEAD_AES_256_CBC_HMAC_SHA256",
                System.Text.Encoding.UTF8.GetBytes(wrappedSuffix),
                new EncryptionKeyWrapMetadata("test", "test", "RSA-OAEP", "test"),
                DateTime.UtcNow);
        }

        [TestMethod]
        public async Task Dispose_DrainsInFlightFireAndForgetWrite()
        {
            // L2 SetAsync hangs on a TCS so the FAF write is in-flight when we call Dispose.
            // Dispose must cancel the disposal CTS, the await in the lambda must throw OCE on
            // the disposal token, the per-task continuation must observe + deregister, and the
            // bounded drain must let Dispose return.
            TaskCompletionSource<byte[]> setBlock = new ();
            Mock<IDistributedCache> mock = new ();
            mock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Returns<string, byte[], DistributedCacheEntryOptions, CancellationToken>(async (k, v, o, ct) =>
                {
                    using (ct.Register(() => setBlock.TrySetCanceled(ct)))
                    {
                        await setBlock.Task;
                    }
                });

            DekCache cache = new (
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mock.Object,
                cacheKeyPrefix: "lifecycle-test");

            cache.SetDekProperties("dek1", NewDek("dek1"));
            Task pending = cache.LastDistributedCacheWriteTask;

            // Dispose should cancel and bound-drain in well under the 5-second timeout.
            DateTime startedAt = DateTime.UtcNow;
            cache.Dispose();
            TimeSpan elapsed = DateTime.UtcNow - startedAt;

            Assert.IsTrue(elapsed < TimeSpan.FromSeconds(4), $"Dispose should drain quickly once disposalToken is cancelled (elapsed={elapsed}).");

            // The pending task should have completed cleanly (cancellation observed inside the
            // lambda's `catch (OperationCanceledException) when (this.disposalToken.IsCancellationRequested)`).
            Assert.IsTrue(pending.IsCompleted, "Pending write task should be observed/completed after drain.");
            Assert.IsFalse(pending.IsFaulted, "Cancellation on disposal must not surface as a fault.");
        }

        [TestMethod]
        public async Task DisposeAsync_DrainsAndCompletesGracefully()
        {
            TaskCompletionSource<byte[]> setBlock = new ();
            Mock<IDistributedCache> mock = new ();
            mock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .Returns<string, byte[], DistributedCacheEntryOptions, CancellationToken>(async (k, v, o, ct) =>
                {
                    using (ct.Register(() => setBlock.TrySetCanceled(ct)))
                    {
                        await setBlock.Task;
                    }
                });

            DekCache cache = new (
                dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                distributedCache: mock.Object,
                cacheKeyPrefix: "lifecycle-test");

            cache.SetDekProperties("dek1", NewDek("dek1"));

            await cache.DisposeAsync();

            Assert.IsTrue(cache.LastDistributedCacheWriteTask.IsCompleted);
        }

        [TestMethod]
        public void Dispose_IsIdempotent()
        {
            DekCache cache = new (dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            cache.Dispose();
            cache.Dispose(); // should not throw
            cache.Dispose();
        }

        [TestMethod]
        public async Task DisposeAsync_IsIdempotent()
        {
            DekCache cache = new (dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));

            await cache.DisposeAsync();
            await cache.DisposeAsync();
        }

        [TestMethod]
        public async Task PostDispose_PublicMethodsThrowObjectDisposedException()
        {
            DekCache cache = new (dekPropertiesTimeToLive: TimeSpan.FromMinutes(30));
            cache.Dispose();

            Assert.ThrowsException<ObjectDisposedException>(
                () => cache.SetDekProperties("dek1", NewDek("dek1")));
            Assert.ThrowsException<ObjectDisposedException>(
                () => cache.SetRawDek("dek1", new InMemoryRawDek(null, TimeSpan.FromMinutes(30))));
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                () => cache.GetOrAddDekPropertiesAsync(
                    "dek1",
                    (id, ctx, ct) => Task.FromResult(NewDek(id)),
                    CosmosDiagnosticsContext.Create(null),
                    CancellationToken.None));
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                () => cache.GetOrAddRawDekAsync(
                    NewDek("dek1"),
                    (props, ctx, ct) => Task.FromResult(new InMemoryRawDek(null, TimeSpan.FromMinutes(30))),
                    CosmosDiagnosticsContext.Create(null),
                    CancellationToken.None));
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                () => cache.RemoveAsync("dek1"));
        }

        [TestMethod]
        public async Task FaultedBackgroundWrite_DoesNotProduceUnobservedTaskException()
        {
            // Force GC observation of any unobserved Task exceptions; subscribe before scheduling
            // the faulted write so the test's listener is the one that would fire.
            int unobserved = 0;
            EventHandler<UnobservedTaskExceptionEventArgs> handler = (sender, args) => Interlocked.Increment(ref unobserved);
            TaskScheduler.UnobservedTaskException += handler;

            try
            {
                Mock<IDistributedCache> mock = new ();
                mock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("synthetic L2 failure"));

                DekCache cache = new (
                    dekPropertiesTimeToLive: TimeSpan.FromMinutes(30),
                    distributedCache: mock.Object,
                    cacheKeyPrefix: "no-unobserved");

                cache.SetDekProperties("dek1", NewDek("dek1"));
                await cache.LastDistributedCacheWriteTask;
                cache.Dispose();

                // Force a GC to provoke any UnobservedTaskException finalizer notifications.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                Assert.AreEqual(0, Volatile.Read(ref unobserved), "No UnobservedTaskException should be raised: per-task ContinueWith observes the exception.");
            }
            finally
            {
                TaskScheduler.UnobservedTaskException -= handler;
            }
        }

        [TestMethod]
        public async Task ProviderDispose_PropagatesToDekCache()
        {
            // Smoke: provider.Dispose() must not throw and must dispose the underlying DekCache
            // (subsequent operations should throw ObjectDisposedException through the DekCache).
            CosmosDataEncryptionKeyProvider provider = new (
                Mock.Of<Microsoft.Data.Encryption.Cryptography.EncryptionKeyStoreProvider>());

            provider.Dispose();
            provider.Dispose(); // idempotent

            DekCache inner = provider.DekCache;
            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                () => inner.RemoveAsync("dek1"));
        }
    }
}
