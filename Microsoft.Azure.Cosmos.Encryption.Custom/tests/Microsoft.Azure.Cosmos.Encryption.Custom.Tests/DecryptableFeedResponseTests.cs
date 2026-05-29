//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using System.Collections.Generic;
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
