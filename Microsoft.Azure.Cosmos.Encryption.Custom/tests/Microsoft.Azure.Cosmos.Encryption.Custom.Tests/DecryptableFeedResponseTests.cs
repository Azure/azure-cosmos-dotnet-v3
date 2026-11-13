//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DecryptableFeedResponseTests
    {
        [TestMethod]
        public async Task DisposeAsync_CascadesToAllAsyncDisposableItems()
        {
            CountingDecryptableItem item1 = new ();
            CountingDecryptableItem item2 = new ();
            CountingDecryptableItem item3 = new ();

            DecryptableFeedResponse<DecryptableItem> response = DecryptableFeedResponse<DecryptableItem>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<DecryptableItem> { item1, item2, item3 });

            Assert.AreEqual(0, item1.DisposeAsyncCallCount);
            Assert.AreEqual(0, item2.DisposeAsyncCallCount);
            Assert.AreEqual(0, item3.DisposeAsyncCallCount);

            await response.DisposeAsync();

            Assert.AreEqual(1, item1.DisposeAsyncCallCount);
            Assert.AreEqual(1, item2.DisposeAsyncCallCount);
            Assert.AreEqual(1, item3.DisposeAsyncCallCount);
        }

        [TestMethod]
        public async Task DisposeAsync_IsIdempotent_DoesNotCascadeMoreThanOnce()
        {
            CountingDecryptableItem item = new ();

            DecryptableFeedResponse<DecryptableItem> response = DecryptableFeedResponse<DecryptableItem>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<DecryptableItem> { item });

            await response.DisposeAsync();
            await response.DisposeAsync();
            await response.DisposeAsync();

            Assert.AreEqual(1, item.DisposeAsyncCallCount);
        }

        [TestMethod]
        public async Task DisposeAsync_WithNullResource_DoesNotThrow()
        {
            DecryptableFeedResponse<DecryptableItem> response = DecryptableFeedResponse<DecryptableItem>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                resource: null);

            await response.DisposeAsync();
        }

        [TestMethod]
        public async Task DisposeAsync_WithEmptyResource_DoesNotThrow()
        {
            DecryptableFeedResponse<DecryptableItem> response = DecryptableFeedResponse<DecryptableItem>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<DecryptableItem>());

            await response.DisposeAsync();
        }

        [TestMethod]
        public async Task DisposeAsync_WithNonAsyncDisposableItems_SkipsThem()
        {
            // Strings do not implement IAsyncDisposable; the cascade should skip them silently.
            // This validates the DataEncryptionKeyFeedIterator path where items are
            // DataEncryptionKeyProperties (which are not IAsyncDisposable).
            DecryptableFeedResponse<string> response = DecryptableFeedResponse<string>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<string> { "one", "two", "three" });

            await response.DisposeAsync();
        }

        [TestMethod]
        public async Task DisposeAsync_MixedDisposableAndNonDisposableItems_DisposesOnlyDisposableOnes()
        {
            // Boxed wrapper so the static-typed Resource is object but items may or may not be IAsyncDisposable.
            CountingDecryptableItem disposableItem = new ();
            object nonDisposableItem = "plain string";

            DecryptableFeedResponse<object> response = DecryptableFeedResponse<object>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<object> { disposableItem, nonDisposableItem, disposableItem });

            await response.DisposeAsync();

            // disposableItem appears twice in the list; cascade calls DisposeAsync each time.
            Assert.AreEqual(2, disposableItem.DisposeAsyncCallCount);
        }

        [TestMethod]
        public async Task DisposeAsync_AfterItemAlreadyDisposed_CascadeStillCallsItemDisposeAsync()
        {
            // The response does not track per-item disposal state; the cascade calls DisposeAsync
            // on every item. It is the item's responsibility to be idempotent.
            CountingDecryptableItem item = new ();

            await item.DisposeAsync();
            Assert.AreEqual(1, item.DisposeAsyncCallCount);

            DecryptableFeedResponse<DecryptableItem> response = DecryptableFeedResponse<DecryptableItem>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<DecryptableItem> { item });

            await response.DisposeAsync();

            Assert.AreEqual(2, item.DisposeAsyncCallCount);
        }

        [TestMethod]
        public async Task DisposeAsync_ResponseIsCastableToIAsyncDisposable()
        {
            // The public XML doc example on DecryptableItem instructs customers to cast the
            // FeedResponse<DecryptableItem> returned by ReadNextAsync() to IAsyncDisposable.
            // This locks in that contract.
            FeedResponse<DecryptableItem> response = DecryptableFeedResponse<DecryptableItem>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<DecryptableItem>());

            Assert.IsInstanceOfType(response, typeof(IAsyncDisposable));

            if (response is IAsyncDisposable disposable)
            {
                await disposable.DisposeAsync();
            }
            else
            {
                Assert.Fail("DecryptableFeedResponse must implement IAsyncDisposable.");
            }
        }

        [TestMethod]
        public async Task DisposeAsync_PropagatesExceptionFromItemDispose()
        {
            ThrowingDecryptableItem throwingItem = new ();

            DecryptableFeedResponse<DecryptableItem> response = DecryptableFeedResponse<DecryptableItem>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<DecryptableItem> { throwingItem });

            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await response.DisposeAsync());

            Assert.AreEqual("simulated dispose failure", ex.Message);
        }

        [TestMethod]
        public async Task DisposeAsync_WhenItemThrows_StillDisposesRemainingItems()
        {
            // Regression test for the cascade-leak bug: if the first item throws on DisposeAsync,
            // the rest of the page must still be drained so their pooled buffers are returned.
            CountingDecryptableItem beforeThrowing = new ();
            ThrowingDecryptableItem throwingItem = new ();
            CountingDecryptableItem afterThrowing1 = new ();
            CountingDecryptableItem afterThrowing2 = new ();

            DecryptableFeedResponse<DecryptableItem> response = DecryptableFeedResponse<DecryptableItem>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<DecryptableItem> { beforeThrowing, throwingItem, afterThrowing1, afterThrowing2 });

            // A single failure surfaces as the original InvalidOperationException (identity preserved
            // via ExceptionDispatchInfo), not wrapped in AggregateException.
            InvalidOperationException ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                async () => await response.DisposeAsync());

            Assert.AreEqual("simulated dispose failure", ex.Message);
            Assert.AreEqual(1, beforeThrowing.DisposeAsyncCallCount, "Items prior to the throwing one must be disposed.");
            Assert.AreEqual(1, afterThrowing1.DisposeAsyncCallCount, "Cascade must continue past the throwing item to release pooled buffers.");
            Assert.AreEqual(1, afterThrowing2.DisposeAsyncCallCount, "Cascade must drain every item after the throwing one.");
        }

        [TestMethod]
        public async Task DisposeAsync_WhenMultipleItemsThrow_AggregatesAndStillDrains()
        {
            CountingDecryptableItem stillDrained = new ();
            ThrowingDecryptableItem throwing1 = new ();
            ThrowingDecryptableItem throwing2 = new ();

            DecryptableFeedResponse<DecryptableItem> response = DecryptableFeedResponse<DecryptableItem>.CreateResponse(
                new ResponseMessage(HttpStatusCode.OK),
                new List<DecryptableItem> { throwing1, stillDrained, throwing2 });

            AggregateException ex = await Assert.ThrowsExceptionAsync<AggregateException>(
                async () => await response.DisposeAsync());

            Assert.AreEqual(2, ex.InnerExceptions.Count, "Both throwing items should be surfaced.");
            Assert.IsTrue(ex.InnerExceptions.All(inner => inner is InvalidOperationException && inner.Message == "simulated dispose failure"));
            Assert.AreEqual(1, stillDrained.DisposeAsyncCallCount, "Non-throwing items between/after throwing ones must still be disposed.");
        }

        private sealed class CountingDecryptableItem : DecryptableItem
        {
            public int DisposeAsyncCallCount { get; private set; }

            public override Task<(T, DecryptionContext)> GetItemAsync<T>()
            {
                throw new NotSupportedException("Test stub: GetItemAsync is not invoked.");
            }

            public override ValueTask DisposeAsync()
            {
                this.DisposeAsyncCallCount++;
                return default;
            }
        }

        private sealed class ThrowingDecryptableItem : DecryptableItem
        {
            public override Task<(T, DecryptionContext)> GetItemAsync<T>()
            {
                throw new NotSupportedException("Test stub: GetItemAsync is not invoked.");
            }

            public override ValueTask DisposeAsync()
            {
                throw new InvalidOperationException("simulated dispose failure");
            }
        }
    }
}
