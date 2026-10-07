//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.Exceptions;
    using Microsoft.Azure.Cosmos.ChangeFeed.FeedManagement;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Tests;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Newtonsoft.Json.Linq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseUpdaterCosmosTests
    {
        [TestMethod]
        public async Task UpdatesLease()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerInternal> mockedItems = new Mock<ContainerInternal>();
            mockedItems.Setup(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreateReplaceResponse("\"updated\""));

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });

            Assert.AreEqual("newHost", updatedLease.Owner);
            Assert.AreEqual("\"updated\"", updatedLease.ConcurrencyToken);
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>((pk) => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task RetriesOnPreconditionFailed()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerInternal> mockedItems = new Mock<ContainerInternal>();
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                });

            mockedItems.SetupSequence(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(new ResponseMessage(HttpStatusCode.PreconditionFailed)))
                .Returns(() => Task.FromResult(CreateReplaceResponse("\"updated\"")));

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });

            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Exactly(2));
            Mock.Get(mockedItems.Object)
                .Verify(items => items.ReadItemStreamAsync(It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        [ExpectedException(typeof(LeaseLostException))]
        public async Task ThrowsAfterMaxRetries()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerInternal> mockedItems = new Mock<ContainerInternal>();
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                });

            mockedItems.Setup(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(new ResponseMessage(HttpStatusCode.PreconditionFailed)));

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            DocumentServiceLease updatedLease = await updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            });
        }

        [TestMethod]
        public async Task ThrowsOnConflict()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerInternal> mockedItems = new Mock<ContainerInternal>();
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                });

            mockedItems.SetupSequence(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(new ResponseMessage(HttpStatusCode.Conflict)))
                .Returns(() => Task.FromResult(CreateReplaceResponse("\"updated\"")));

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            LeaseLostException leaseLost = await Assert.ThrowsExceptionAsync<LeaseLostException>(() => updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            }));

            Assert.IsTrue(leaseLost.InnerException is CosmosException innerCosmosException
                && innerCosmosException.StatusCode == HttpStatusCode.Conflict);
        }

        [TestMethod]
        public async Task ThrowsOnNotFoundReplace()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerInternal> mockedItems = new Mock<ContainerInternal>();
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.OK)
                {
                    Content = new CosmosJsonDotNetSerializer().ToStream(leaseToUpdate)
                });

            mockedItems.SetupSequence(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(new ResponseMessage(HttpStatusCode.NotFound)))
                .Returns(() => Task.FromResult(CreateReplaceResponse("\"updated\"")));

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            LeaseLostException leaseLost = await Assert.ThrowsExceptionAsync<LeaseLostException>(() => updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            }));

            Assert.IsTrue(leaseLost.InnerException is CosmosException innerCosmosException
                && innerCosmosException.StatusCode == HttpStatusCode.NotFound);
        }

        [TestMethod]
        public async Task ThrowsOnNotFoundRead()
        {
            string itemId = "1";
            Cosmos.PartitionKey partitionKey = new Cosmos.PartitionKey("1");
            DocumentServiceLeaseCore leaseToUpdate = new DocumentServiceLeaseCore();

            Mock<ContainerInternal> mockedItems = new Mock<ContainerInternal>();
            mockedItems.Setup(i => i.ReadItemStreamAsync(
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.NotFound));

            mockedItems.SetupSequence(i => i.ReplaceItemStreamAsync(
                It.IsAny<Stream>(),
                It.Is<string>((id) => id == itemId),
                It.Is<Cosmos.PartitionKey>(pk => pk.Equals(partitionKey)),
                It.IsAny<ItemRequestOptions>(),
                It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult(new ResponseMessage(HttpStatusCode.PreconditionFailed)))
                .Returns(() => Task.FromResult(CreateReplaceResponse("\"updated\"")));

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(DocumentServiceLeaseUpdaterCosmosTests.GetMockedContainer(mockedItems));
            LeaseLostException leaseLost = await Assert.ThrowsExceptionAsync<LeaseLostException>(() => updater.UpdateLeaseAsync(leaseToUpdate, itemId, partitionKey, serverLease =>
            {
                serverLease.Owner = "newHost";
                return serverLease;
            }));

            Assert.IsTrue(leaseLost.InnerException is CosmosException innerCosmosException
                && innerCosmosException.StatusCode == HttpStatusCode.NotFound);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task SequentialCheckpointsUseResponseETags(bool useEpkLease)
        {
            DocumentServiceLease lease = CreateLease(useEpkLease);
            JObject serverDocument = JObject.FromObject(lease);
            int replaces = 0;
            int conflicts = 0;
            int reads = 0;
            List<string> requestETags = new List<string>();
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            container.Setup(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), lease.Id, new PartitionKey(lease.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string id, PartitionKey pk, ItemRequestOptions options, CancellationToken token) =>
                {
                    Assert.AreEqual(false, options.EnableContentResponseOnWrite);
                    requestETags.Add(options.IfMatchEtag);
                    JObject submitted = ReadDocument(stream);
                    Assert.AreEqual(options.IfMatchEtag, (string)submitted["_etag"]);
                    if (options.IfMatchEtag != (string)serverDocument["_etag"])
                    {
                        conflicts++;
                        return new ResponseMessage(HttpStatusCode.PreconditionFailed);
                    }

                    replaces++;
                    Assert.AreEqual($"continuation-{replaces}", (string)submitted["ContinuationToken"]);
                    serverDocument = submitted;
                    serverDocument["_etag"] = $"\"etag-{replaces}\"";
                    return CreateReplaceResponse((string)serverDocument["_etag"]);
                });
            container.Setup(c => c.ReadItemStreamAsync(
                lease.Id, new PartitionKey(lease.Id), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    reads++;
                    return CreateReadResponse(serverDocument);
                });

            PartitionCheckpointerCore checkpointer = new PartitionCheckpointerCore(
                new DocumentServiceLeaseCheckpointerCore(
                    new DocumentServiceLeaseUpdaterCosmos(container.Object),
                    new PartitionedByIdCollectionRequestOptionsFactory()),
                lease);

            for (int checkpoint = 1; checkpoint <= 100; checkpoint++)
            {
                await checkpointer.CheckpointPartitionAsync($"continuation-{checkpoint}");
            }

            Assert.AreEqual(100, replaces);
            Assert.AreEqual(0, conflicts);
            Assert.AreEqual(0, reads);
            Assert.AreEqual(100, requestETags.Count);
            for (int checkpoint = 0; checkpoint < requestETags.Count; checkpoint++)
            {
                Assert.AreEqual($"\"etag-{checkpoint}\"", requestETags[checkpoint]);
            }

            Assert.AreEqual("\"etag-0\"", lease.ConcurrencyToken);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task PropertyUpdateReturnsSubmittedSnapshotWhileOriginalChanges(bool useEpkLease)
        {
            DocumentServiceLease lease = CreateLease(useEpkLease);
            TaskCompletionSource<ResponseMessage> pendingResponse = new TaskCompletionSource<ResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            JObject submitted = null;
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            container.Setup(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), lease.Id, new PartitionKey(lease.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .Returns((Stream stream, string id, PartitionKey pk, ItemRequestOptions options, CancellationToken token) =>
                {
                    Assert.AreEqual(false, options.EnableContentResponseOnWrite);
                    Assert.AreEqual("\"etag-0\"", options.IfMatchEtag);
                    submitted = ReadDocument(stream);
                    return pendingResponse.Task;
                });

            DocumentServiceLeaseManagerCosmos manager = CreateManager(container.Object);
            Task<DocumentServiceLease> update = manager.UpdatePropertiesAsync(lease);
            Assert.IsNotNull(submitted);
            Assert.IsFalse(update.IsCompleted);

            // UpdatePropertiesAsync reattaches the caller's dictionary in its update delegate.
            lease.Properties["key"] = "changed while writing";
            lease.FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "CC", true, false));
            lease.ContinuationToken = "changed while writing";
            lease.Owner = "another host";
            lease.Timestamp = DateTime.UtcNow.AddHours(1);
            pendingResponse.SetResult(CreateReplaceResponse("\"etag-1\""));
            DocumentServiceLease result = await update;

            Assert.AreNotSame(lease, result);
            Assert.AreNotSame(lease.Properties, result.Properties);
            Assert.AreNotSame(lease.FeedRange, result.FeedRange);
            Assert.AreNotSame(((FeedRangeEpk)lease.FeedRange).Range, ((FeedRangeEpk)result.FeedRange).Range);
            submitted["_etag"] = "\"etag-1\"";
            Assert.IsTrue(JToken.DeepEquals(submitted, JObject.FromObject(result)));
            Assert.AreEqual("\"etag-0\"", lease.ConcurrencyToken);
            Assert.AreEqual("changed while writing", lease.Properties["key"]);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task UpdateDelegateDoesNotOwnWriteSnapshot(bool useEpkLease)
        {
            DocumentServiceLease original = CreateLease(useEpkLease);
            DocumentServiceLease delegateLease = null;
            JObject submitted = null;
            TaskCompletionSource<ResponseMessage> pendingResponse = new TaskCompletionSource<ResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            container.Setup(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), original.Id, new PartitionKey(original.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .Returns((Stream stream, string id, PartitionKey pk, ItemRequestOptions options, CancellationToken token) =>
                {
                    Assert.AreEqual("\"etag-0\"", options.IfMatchEtag);
                    submitted = ReadDocument(stream);
                    return pendingResponse.Task;
                });

            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(container.Object);
            Task<DocumentServiceLease> update = updater.UpdateLeaseAsync(
                original, original.Id, new PartitionKey(original.Id), lease =>
                {
                    delegateLease = lease;
                    lease.Owner = "new owner";
                    lease.ContinuationToken = "new continuation";
                    lease.Properties = original.Properties;
                    lease.FeedRange = original.FeedRange;
                    return lease;
                });

            Assert.IsNotNull(submitted);
            Assert.AreNotSame(original, delegateLease);
            Assert.AreEqual("host", original.Owner);
            Assert.AreEqual("initial", original.ContinuationToken);
            delegateLease.ContinuationToken = "delegate mutation";
            delegateLease.Properties["key"] = "delegate mutation";
            delegateLease.FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "CC", true, false));
            pendingResponse.SetResult(CreateReplaceResponse("\"etag-1\""));
            DocumentServiceLease result = await update;

            Assert.AreNotSame(delegateLease, result);
            Assert.AreEqual("\"etag-0\"", original.ConcurrencyToken);
            Assert.AreEqual("\"etag-0\"", delegateLease.ConcurrencyToken);
            submitted["_etag"] = "\"etag-1\"";
            Assert.IsTrue(JToken.DeepEquals(submitted, JObject.FromObject(result)));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ConcurrentCheckpointsKeepTheirOwnSnapshots(bool useEpkLease)
        {
            DocumentServiceLease original = CreateLease(useEpkLease);
            JObject serverDocument = JObject.FromObject(original);
            JObject firstDocument = null;
            int replaces = 0;
            int conflicts = 0;
            int reads = 0;
            TaskCompletionSource<ResponseMessage> firstResponse = new TaskCompletionSource<ResponseMessage>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            container.Setup(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), original.Id, new PartitionKey(original.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .Returns((Stream stream, string id, PartitionKey pk, ItemRequestOptions options, CancellationToken token) =>
                {
                    Assert.AreEqual(false, options.EnableContentResponseOnWrite);
                    if (options.IfMatchEtag != (string)serverDocument["_etag"])
                    {
                        conflicts++;
                        return Task.FromResult(new ResponseMessage(HttpStatusCode.PreconditionFailed));
                    }

                    serverDocument = ReadDocument(stream);
                    serverDocument["_etag"] = $"\"etag-{++replaces}\"";
                    if (replaces == 1)
                    {
                        firstDocument = serverDocument;
                        return firstResponse.Task;
                    }

                    return Task.FromResult(CreateReplaceResponse((string)serverDocument["_etag"]));
                });
            container.Setup(c => c.ReadItemStreamAsync(
                original.Id, new PartitionKey(original.Id), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    reads++;
                    return CreateReadResponse(serverDocument);
                });
            DocumentServiceLeaseCheckpointerCore checkpointer = new DocumentServiceLeaseCheckpointerCore(
                new DocumentServiceLeaseUpdaterCosmos(container.Object),
                new PartitionedByIdCollectionRequestOptionsFactory());

            Task<DocumentServiceLease> firstCheckpoint = checkpointer.CheckpointAsync(original, "first");
            Assert.IsNotNull(firstDocument);
            Assert.IsFalse(firstCheckpoint.IsCompleted);
            DocumentServiceLease second = await checkpointer.CheckpointAsync(original, "second");
            firstResponse.SetResult(CreateReplaceResponse("\"etag-1\""));
            DocumentServiceLease first = await firstCheckpoint;

            Assert.IsTrue(JToken.DeepEquals(firstDocument, JObject.FromObject(first)));
            Assert.IsTrue(JToken.DeepEquals(serverDocument, JObject.FromObject(second)));
            Assert.AreEqual("first", first.ContinuationToken);
            Assert.AreEqual("\"etag-1\"", first.ConcurrencyToken);
            Assert.AreEqual("second", second.ContinuationToken);
            Assert.AreEqual("\"etag-2\"", second.ConcurrencyToken);
            Assert.AreEqual("initial", original.ContinuationToken);
            Assert.AreEqual("\"etag-0\"", original.ConcurrencyToken);
            Assert.AreEqual(2, replaces);
            Assert.AreEqual(1, conflicts);
            Assert.AreEqual(1, reads);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task CheckpointRevalidatesOwnerAfterConcurrentUpdate(bool useEpkLease, bool ownerChanges)
        {
            DocumentServiceLease lease = CreateLease(useEpkLease);
            JObject serverDocument = JObject.FromObject(lease);
            serverDocument["_etag"] = "\"competing\"";
            serverDocument["properties"]["key"] = "competing properties";
            if (ownerChanges)
            {
                serverDocument["Owner"] = "another host";
            }

            int replaces = 0;
            int conflicts = 0;
            int reads = 0;
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            container.Setup(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), lease.Id, new PartitionKey(lease.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string id, PartitionKey pk, ItemRequestOptions options, CancellationToken token) =>
                {
                    replaces++;
                    Assert.AreEqual(false, options.EnableContentResponseOnWrite);
                    if (options.IfMatchEtag != (string)serverDocument["_etag"])
                    {
                        conflicts++;
                        return new ResponseMessage(HttpStatusCode.PreconditionFailed);
                    }

                    Assert.AreEqual("\"competing\"", options.IfMatchEtag);
                    serverDocument = ReadDocument(stream);
                    serverDocument["_etag"] = "\"checkpoint\"";
                    return CreateReplaceResponse("\"checkpoint\"");
                });
            container.Setup(c => c.ReadItemStreamAsync(
                lease.Id, new PartitionKey(lease.Id), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    reads++;
                    return CreateReadResponse(serverDocument);
                });
            DocumentServiceLeaseCheckpointerCore checkpointer = new DocumentServiceLeaseCheckpointerCore(
                new DocumentServiceLeaseUpdaterCosmos(container.Object),
                new PartitionedByIdCollectionRequestOptionsFactory());

            if (ownerChanges)
            {
                await Assert.ThrowsExceptionAsync<LeaseLostException>(() => checkpointer.CheckpointAsync(lease, "checkpoint"));
                Assert.AreEqual(1, replaces);
                Assert.AreEqual("initial", (string)serverDocument["ContinuationToken"]);
            }
            else
            {
                DocumentServiceLease result = await checkpointer.CheckpointAsync(lease, "checkpoint");
                Assert.AreEqual(2, replaces);
                Assert.AreEqual("\"checkpoint\"", result.ConcurrencyToken);
                Assert.AreEqual("checkpoint", result.ContinuationToken);
                Assert.AreEqual("competing properties", result.Properties["key"]);
                Assert.AreEqual("host", result.Owner);
            }

            Assert.AreEqual(1, conflicts);
            Assert.AreEqual(1, reads);
            Assert.AreEqual("\"etag-0\"", lease.ConcurrencyToken);
            Assert.AreEqual("initial", lease.ContinuationToken);
        }

        [DataTestMethod]
        [DataRow(false, null)]
        [DataRow(true, null)]
        [DataRow(false, "")]
        [DataRow(true, "")]
        [DataRow(false, " ")]
        [DataRow(true, " ")]
        public async Task SuccessfulReplaceWithoutETagFailsExplicitly(bool useEpkLease, string etag)
        {
            DocumentServiceLease lease = CreateLease(useEpkLease);
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            container.Setup(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), lease.Id, new PartitionKey(lease.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => CreateReplaceResponse(etag));
            DocumentServiceLeaseCheckpointerCore checkpointer = new DocumentServiceLeaseCheckpointerCore(
                new DocumentServiceLeaseUpdaterCosmos(container.Object),
                new PartitionedByIdCollectionRequestOptionsFactory());

            InvalidOperationException exception = await Assert.ThrowsExceptionAsync<InvalidOperationException>(
                () => checkpointer.CheckpointAsync(lease, "checkpoint"));

            StringAssert.Contains(exception.Message, "ETag");
            Assert.AreEqual("\"etag-0\"", lease.ConcurrencyToken);
            Assert.AreEqual("initial", lease.ContinuationToken);
            container.Verify(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), lease.Id, new PartitionKey(lease.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            container.VerifyNoOtherCalls();
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task LeaseManagerOperationsKeepConditionalETags(bool useEpkLease)
        {
            DocumentServiceLease original = CreateLease(useEpkLease);
            original.Owner = "previous host";
            JObject serverDocument = JObject.FromObject(original);
            int replaces = 0;
            int reads = 0;
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            container.Setup(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), original.Id, new PartitionKey(original.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Stream stream, string id, PartitionKey pk, ItemRequestOptions options, CancellationToken token) =>
                {
                    Assert.AreEqual(false, options.EnableContentResponseOnWrite);
                    Assert.AreEqual((string)serverDocument["_etag"], options.IfMatchEtag);
                    serverDocument = ReadDocument(stream);
                    serverDocument["_etag"] = $"\"etag-{++replaces}\"";
                    return CreateReplaceResponse((string)serverDocument["_etag"]);
                });
            container.Setup(c => c.ReadItemStreamAsync(
                original.Id, new PartitionKey(original.Id), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    reads++;
                    return CreateReadResponse(serverDocument);
                });
            DocumentServiceLeaseManagerCosmos manager = CreateManager(container.Object);

            DocumentServiceLease acquired = await manager.AcquireAsync(original);
            Assert.AreEqual("host", acquired.Owner);
            Assert.AreEqual("\"etag-1\"", acquired.ConcurrencyToken);
            Assert.AreEqual("previous host", original.Owner);
            DocumentServiceLease renewed = await manager.RenewAsync(acquired);
            Assert.AreEqual("\"etag-2\"", renewed.ConcurrencyToken);
            renewed.Properties["key"] = "updated";
            DocumentServiceLease updated = await manager.UpdatePropertiesAsync(renewed);
            Assert.AreEqual("\"etag-3\"", updated.ConcurrencyToken);
            Assert.AreEqual("updated", updated.Properties["key"]);
            await manager.ReleaseAsync(updated);

            Assert.AreEqual(4, replaces);
            Assert.AreEqual(2, reads);
            Assert.AreEqual(JTokenType.Null, serverDocument["Owner"].Type);
            Assert.AreEqual("initial", (string)serverDocument["ContinuationToken"]);
            Assert.AreEqual("\"etag-0\"", original.ConcurrencyToken);
            Assert.AreEqual("\"etag-1\"", acquired.ConcurrencyToken);
            Assert.AreEqual("\"etag-2\"", renewed.ConcurrencyToken);
            Assert.AreEqual("\"etag-3\"", updated.ConcurrencyToken);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task CancelledUpdateDoesNotChangeCachedLease(bool useEpkLease)
        {
            DocumentServiceLease lease = CreateLease(useEpkLease);
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            DocumentServiceLeaseUpdaterCosmos updater = new DocumentServiceLeaseUpdaterCosmos(container.Object);

            DocumentServiceLease result = await updater.UpdateLeaseAsync(
                lease, lease.Id, new PartitionKey(lease.Id), workingLease =>
                {
                    workingLease.Owner = "not persisted";
                    workingLease.Properties["key"] = "not persisted";
                    return null;
                });

            Assert.IsNull(result);
            Assert.AreEqual("host", lease.Owner);
            Assert.AreEqual("value", lease.Properties["key"]);
            container.VerifyNoOtherCalls();
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task UnexpectedReplaceErrorIsPropagated(bool useEpkLease)
        {
            DocumentServiceLease lease = CreateLease(useEpkLease);
            Mock<ContainerInternal> container = new Mock<ContainerInternal>(MockBehavior.Strict);
            container.Setup(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), lease.Id, new PartitionKey(lease.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new ResponseMessage(HttpStatusCode.InternalServerError));
            DocumentServiceLeaseCheckpointerCore checkpointer = new DocumentServiceLeaseCheckpointerCore(
                new DocumentServiceLeaseUpdaterCosmos(container.Object),
                new PartitionedByIdCollectionRequestOptionsFactory());

            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => checkpointer.CheckpointAsync(lease, "checkpoint"));

            Assert.AreEqual(HttpStatusCode.InternalServerError, exception.StatusCode);
            Assert.AreEqual("\"etag-0\"", lease.ConcurrencyToken);
            Assert.AreEqual("initial", lease.ContinuationToken);
            container.Verify(c => c.ReplaceItemStreamAsync(
                It.IsAny<Stream>(), lease.Id, new PartitionKey(lease.Id),
                It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
            container.VerifyNoOtherCalls();
        }

        private static DocumentServiceLease CreateLease(bool useEpkLease)
        {
            DocumentServiceLease lease = useEpkLease
                ? new DocumentServiceLeaseCoreEpk { LeaseId = "lease", LeaseToken = "AA-BB", ETag = "\"etag-0\"" }
                : new DocumentServiceLeaseCore { LeaseId = "lease", LeaseToken = "0", ETag = "\"etag-0\"" };
            lease.Owner = "host";
            lease.ContinuationToken = "initial";
            lease.FeedRange = new FeedRangeEpk(new Documents.Routing.Range<string>("AA", "BB", true, false));
            lease.Properties["key"] = "value";
            return lease;
        }

        private static DocumentServiceLeaseManagerCosmos CreateManager(ContainerInternal container)
        {
            return new DocumentServiceLeaseManagerCosmos(
                Mock.Of<ContainerInternal>(),
                container,
                new DocumentServiceLeaseUpdaterCosmos(container),
                new DocumentServiceLeaseStoreManagerOptions { HostName = "host" },
                new PartitionedByIdCollectionRequestOptionsFactory());
        }

        private static JObject ReadDocument(Stream stream)
        {
            using StreamReader reader = new StreamReader(stream, leaveOpen: true);
            return JObject.Parse(reader.ReadToEnd());
        }

        private static ResponseMessage CreateReadResponse(JObject document)
        {
            return new ResponseMessage(HttpStatusCode.OK)
            {
                Content = new CosmosJsonDotNetSerializer().ToStream(document),
                Headers = { ETag = (string)document["_etag"] }
            };
        }

        private static ResponseMessage CreateReplaceResponse(string etag)
        {
            return new ResponseMessage(HttpStatusCode.OK)
            {
                Headers = { ETag = etag }
            };
        }

        private static ContainerInternal GetMockedContainer(Mock<ContainerInternal> mockedContainer)
        {
            mockedContainer.Setup(c => c.LinkUri).Returns("/dbs/myDb/colls/myColl");
            return mockedContainer.Object;
        }

        private static CosmosClient GetMockedClient()
        {
            DocumentClient documentClient = new MockDocumentClient();

            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Guid.NewGuid().ToString());

            return cosmosClientBuilder.Build(documentClient);
        }
    }
}