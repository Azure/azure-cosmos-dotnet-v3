//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if PREVIEW
namespace Microsoft.Azure.Cosmos.Tests.FeedRange
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using FeedRange = Microsoft.Azure.Cosmos.FeedRange;
    using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
    using PartitionKeyDefinitionVersion = Microsoft.Azure.Documents.PartitionKeyDefinitionVersion;

    [TestClass]
    public class TryGetFeedRangeForPartitionKeyTests
    {
        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void IndexPreservesIdentityAndHandlesBoundariesAndGaps(bool lengthAware)
        {
            FeedRange first = CreateRange("", "40");
            FeedRange second = CreateRange("60", "80", maxInclusive: true);
            FeedRange third = FeedRange.FromJsonString(CreateRange("90", "FF").ToJsonString());
            FeedRange[] snapshot = { third, first, second };
            ContainerCore.FeedRangeEpkIndex index = ContainerCore.FeedRangeEpkIndex.Create(snapshot, lengthAware, default);

            Assert.AreSame(first, index.TryGetFeedRange(""));
            Assert.AreSame(first, index.TryGetFeedRange("20"));
            Assert.IsNull(index.TryGetFeedRange("40"));
            Assert.IsNull(index.TryGetFeedRange("50"));
            Assert.AreSame(second, index.TryGetFeedRange("60"));
            Assert.AreSame(second, index.TryGetFeedRange("80"));
            Assert.IsNull(index.TryGetFeedRange("81"));
            Assert.AreSame(third, index.TryGetFeedRange("90"));
            Assert.IsNull(index.TryGetFeedRange("FF"));
            CollectionAssert.AreEqual(new[] { third, first, second }, snapshot);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void IndexHonorsConfiguredLengthAwareComparer(bool lengthAware)
        {
            const string shortBoundary = "06AB34CFE4E482236BCACBBF50E234AB";
            string paddedBoundary = shortBoundary + new string('0', 32);
            FeedRange left = CreateRange("", paddedBoundary);
            FeedRange right = CreateRange(paddedBoundary, "FF");
            ContainerCore.FeedRangeEpkIndex index = ContainerCore.FeedRangeEpkIndex.Create(new[] { right, left }, lengthAware, default);

            Assert.AreSame(lengthAware ? right : left, index.TryGetFeedRange(shortBoundary));
            Assert.AreSame(right, index.TryGetFeedRange(paddedBoundary));
            Assert.AreSame(right, index.TryGetFeedRange("0800000000000000000000000000000000000000000000000000000000000000"));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void IndexSupportsPointRangesAndEmptySnapshots(bool lengthAware)
        {
            FeedRange point = CreateRange("40", "40", maxInclusive: true);
            ContainerCore.FeedRangeEpkIndex index = ContainerCore.FeedRangeEpkIndex.Create(new[] { point }, lengthAware, default);
            Assert.AreSame(point, index.TryGetFeedRange("40"));
            Assert.IsNull(index.TryGetFeedRange("41"));
            Assert.IsNull(ContainerCore.FeedRangeEpkIndex.Create(Array.Empty<FeedRange>(), lengthAware, default).TryGetFeedRange("40"));
            Assert.ThrowsException<ArgumentNullException>(() => index.TryGetFeedRange(null));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void IndexRejectsInvalidAndOverlappingSnapshots(bool lengthAware)
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            client.ClientContext.ClientOptions.UseLengthAwareRangeComparer = lengthAware;
            Mock<ContainerCore> container = CreateContainer(client, CreateDefinition(PartitionKeyDefinitionVersion.V2));
            IReadOnlyList<FeedRange>[] invalidSnapshots =
            {
                new FeedRange[] { null },
                new[] { FeedRange.FromPartitionKey(new PartitionKey("value")) },
                new FeedRange[] { new FeedRangePartitionKeyRange("0") },
                new FeedRange[] { new JsonFeedRange("not-json") },
                new FeedRange[] { new JsonFeedRange("null") },
                new FeedRange[] { new JsonFeedRange("{\"Range\":{\"min\":\"20\",\"max\":\"40\",\"isMinInclusive\":false}}") },
                new FeedRange[] { new JsonFeedRange("{\"Range\":{\"min\":null,\"max\":\"40\"}}") },
                new[] { CreateRange("40", "20") },
                new[] { CreateRange("40", "40") },
                new[] { CreateRange("20G", "40") },
                new[] { CreateRange("FF", "FFFF") },
                new[] { CreateRange("", "60"), CreateRange("40", "80") },
                new[] { CreateRange("40", "80"), CreateRange("40", "80") },
                new[] { CreateRange("", "40", maxInclusive: true), CreateRange("40", "80") },
            };

            foreach (IReadOnlyList<FeedRange> snapshot in invalidSnapshots)
            {
                ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                    () => container.Object.CreateFeedRangeSnapshot(snapshot));
                Assert.AreEqual("feedRanges", exception.ParamName);
            }

            Assert.ThrowsException<ArgumentNullException>(() => container.Object.CreateFeedRangeSnapshot(null));
        }

        [TestMethod]
        public async Task LookupResolvesScalarKeysUsingBothHashVersions()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            foreach (PartitionKeyDefinitionVersion version in new[] { PartitionKeyDefinitionVersion.V1, PartitionKeyDefinitionVersion.V2 })
            {
                PartitionKeyDefinition definition = CreateDefinition(version);
                Mock<ContainerCore> container = CreateContainer(client, definition);
                PartitionKey[] keys = { new PartitionKey("WA"), new PartitionKey(42), new PartitionKey(true), PartitionKey.Null, default };
                foreach (PartitionKey key in keys)
                {
                    string epk = (key.InternalKey ?? PartitionKey.Null.InternalKey).GetEffectivePartitionKeyString(definition);
                    FeedRange owner = CreateRange(epk, "FF");
                    FeedRange before = CreateRange("", epk);
                    FeedRangeSnapshot snapshot = container.Object.CreateFeedRangeSnapshot(new[] { owner, before });
                    Assert.AreSame(owner, await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, key));
                    Assert.IsNull(await container.Object.TryGetFeedRangeForPartitionKeyAsync(container.Object.CreateFeedRangeSnapshot(new[] { before }), key));
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task LookupUsesClientComparerConfiguration(bool lengthAware)
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            client.ClientContext.ClientOptions.UseLengthAwareRangeComparer = lengthAware;
            PartitionKeyDefinition definition = CreateDefinition(PartitionKeyDefinitionVersion.V2);
            Mock<ContainerCore> container = CreateContainer(client, definition);
            PartitionKey key = new PartitionKey("value");
            string paddedBoundary = key.InternalKey.GetEffectivePartitionKeyString(definition) + new string('0', 32);
            FeedRange left = CreateRange("", paddedBoundary);
            FeedRange right = CreateRange(paddedBoundary, "FF");
            FeedRangeSnapshot snapshot = container.Object.CreateFeedRangeSnapshot(new[] { right, left });
            client.ClientContext.ClientOptions.UseLengthAwareRangeComparer = !lengthAware;
            Assert.AreSame(lengthAware ? right : left,
                await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, key));
        }

        [TestMethod]
        public async Task LookupRequiresCompleteHierarchicalKeys()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            PartitionKeyDefinition definition = CreateDefinition(PartitionKeyDefinitionVersion.V2, "/city", "/state");
            definition.Kind = PartitionKind.MultiHash;
            Mock<ContainerCore> container = CreateContainer(client, definition);
            PartitionKey complete = new PartitionKeyBuilder().Add("Redmond").Add("WA").Build();
            FeedRange owner = FeedRangeEpk.FullRange;
            FeedRangeSnapshot snapshot = container.Object.CreateFeedRangeSnapshot(new[] { owner });
            Assert.AreSame(owner, await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, complete));

            foreach (PartitionKey incomplete in new[] { new PartitionKey("Redmond"), PartitionKey.Null, default, PartitionKey.None })
            {
                ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                    () => container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, incomplete));
                Assert.AreEqual("partitionKey", exception.ParamName);
            }

            PartitionKey extra = new PartitionKeyBuilder().Add("Redmond").Add("WA").Add("US").Build();
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, extra));
        }

        [TestMethod]
        public async Task LookupResolvesMissingKeysWithContainerMetadata()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            foreach (bool legacy in new[] { false, true })
            {
                PartitionKeyDefinition definition = legacy
                    ? CreateDefinition(PartitionKeyDefinitionVersion.V1)
                    : CreateDefinition(PartitionKeyDefinitionVersion.V2);
                if (legacy)
                {
                    definition.Paths = new Collection<string>();
                }
                Mock<ContainerCore> container = CreateContainer(client, definition);
                PartitionKeyInternal none = legacy ? PartitionKeyInternal.Empty : PartitionKeyInternal.Undefined;
                container.Setup(c => c.GetNonePartitionKeyValueAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(none);
                string epk = none.GetEffectivePartitionKeyString(definition);
                FeedRange owner = CreateRange(epk, "FF");

                Assert.AreSame(owner, await container.Object.TryGetFeedRangeForPartitionKeyAsync(container.Object.CreateFeedRangeSnapshot(new[] { owner }), PartitionKey.None));
                container.Verify(c => c.GetNonePartitionKeyValueAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [TestMethod]
        public async Task LookupDoesNotReadSnapshotSourceAgain()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<ContainerCore> container = CreateContainer(client, CreateDefinition(PartitionKeyDefinitionVersion.V2));
            FeedRange[] ranges = Enumerable.Range(0, 4000)
                .Select(i => CreateRange(i == 0 ? "" : i.ToString("X32"), i == 3999 ? "FF" : (i + 1).ToString("X32")))
                .ToArray();
            CountingSnapshot source = new CountingSnapshot(ranges);
            FeedRangeSnapshot snapshot = container.Object.CreateFeedRangeSnapshot(source);
            FeedRange expected = ranges[3999];
            Assert.AreSame(expected, await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")));
            Assert.AreEqual(ranges.Length, source.Reads);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreSame(expected, await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")));
            }

            Assert.AreEqual(ranges.Length, source.Reads, "Lookup must not scan or revalidate the caller list.");
            CountingSnapshot replacement = new CountingSnapshot(ranges);
            Assert.AreSame(expected, await container.Object.TryGetFeedRangeForPartitionKeyAsync(container.Object.CreateFeedRangeSnapshot(replacement), new PartitionKey("value")));
            Assert.AreEqual(ranges.Length, replacement.Reads);
        }

        [TestMethod]
        public async Task LookupPreservesOriginalCustomRangeAndSupportsConcurrentCalls()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<ContainerCore> container = CreateContainer(client, CreateDefinition(PartitionKeyDefinitionVersion.V2));
            JsonFeedRange original = new JsonFeedRange(FeedRangeEpk.FullRange.ToJsonString());
            FeedRangeSnapshot snapshot = container.Object.CreateFeedRangeSnapshot(new[] { original });
            FeedRange[] results = await Task.WhenAll(Enumerable.Range(0, 20).Select(
                _ => Task.Run(() => container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")))));
            foreach (FeedRange result in results)
            {
                Assert.AreSame(original, result);
            }

            Assert.AreSame(original, await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")));
            Assert.AreEqual(1, original.Serializations);
        }

        [TestMethod]
        [DataRow("Clear")]
        [DataRow("Replace")]
        [DataRow("Remove")]
        [DataRow("Reorder")]
        [DataRow("Add")]
        public async Task SnapshotRetainsMembershipWhenSourceListChanges(string mutation)
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            PartitionKeyDefinition definition = CreateDefinition(PartitionKeyDefinitionVersion.V2);
            Mock<ContainerCore> container = CreateContainer(client, definition);
            PartitionKey key = new PartitionKey("value");
            string epk = key.InternalKey.GetEffectivePartitionKeyString(definition);
            FeedRange owner = CreateRange(epk, "FF");
            FeedRange before = CreateRange("", epk);
            List<FeedRange> source = new List<FeedRange> { owner, before };
            FeedRangeSnapshot snapshot = container.Object.CreateFeedRangeSnapshot(source);
            CollectionAssert.AreEqual(new[] { owner, before }, source);

            switch (mutation)
            {
                case "Clear":
                    source.Clear();
                    break;
                case "Replace":
                    source[0] = CreateRange(epk, "FF");
                    break;
                case "Remove":
                    source.Remove(owner);
                    break;
                case "Reorder":
                    source.Reverse();
                    break;
                case "Add":
                    source.Add(FeedRangeEpk.FullRange);
                    break;
                default:
                    Assert.Fail("Unexpected source mutation.");
                    break;
            }

            Assert.AreSame(owner, await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, key));
            if (mutation == "Replace")
            {
                FeedRangeSnapshot updated = container.Object.CreateFeedRangeSnapshot(source);
                Assert.AreSame(source[0], await container.Object.TryGetFeedRangeForPartitionKeyAsync(updated, key));
            }
        }

        [TestMethod]
        public async Task SnapshotRetainsCapturedBoundariesWhenOriginalRepresentationChanges()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            PartitionKeyDefinition definition = CreateDefinition(PartitionKeyDefinitionVersion.V2);
            Mock<ContainerCore> container = CreateContainer(client, definition);
            PartitionKey key = new PartitionKey("value");
            string epk = key.InternalKey.GetEffectivePartitionKeyString(definition);
            JsonFeedRange original = new JsonFeedRange(CreateRange(epk, "FF").ToJsonString());
            FeedRangeSnapshot snapshot = container.Object.CreateFeedRangeSnapshot(new[] { original });
            original.Json = CreateRange("", epk).ToJsonString();

            Assert.AreSame(original, await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, key));
            Assert.AreEqual(1, original.Serializations);
            FeedRangeSnapshot updated = container.Object.CreateFeedRangeSnapshot(new[] { original });
            Assert.IsNull(await container.Object.TryGetFeedRangeForPartitionKeyAsync(updated, key));
            Assert.AreSame(original, await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, key));
            Assert.AreEqual(2, original.Serializations);
        }

        [TestMethod]
        public async Task LookupUsesParentSnapshotWithoutConsultingCurrentRoutingMap()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            PartitionKeyDefinition definition = CreateDefinition(PartitionKeyDefinitionVersion.V2);
            Mock<ContainerCore> container = CreateContainer(client, definition);
            PartitionKey key = new PartitionKey("value");
            string epk = key.InternalKey.GetEffectivePartitionKeyString(definition);
            FeedRange parent = FeedRange.FromJsonString(FeedRangeEpk.FullRange.ToJsonString());
            FeedRangeSnapshot oldSnapshot = container.Object.CreateFeedRangeSnapshot(new[] { parent });
            FeedRange[] children = { CreateRange("", epk), CreateRange(epk, "FF") };
            Assert.AreSame(parent, await container.Object.TryGetFeedRangeForPartitionKeyAsync(oldSnapshot, key));
            Assert.AreSame(children[1], await container.Object.TryGetFeedRangeForPartitionKeyAsync(container.Object.CreateFeedRangeSnapshot(children), key));
            Assert.AreSame(parent, await container.Object.TryGetFeedRangeForPartitionKeyAsync(oldSnapshot, key));
            container.Verify(c => c.GetRoutingMapAsync(It.IsAny<CancellationToken>()), Times.Never);
            container.Verify(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task CreationAndLookupHonorCancellationWithoutPublishingFailedSnapshots()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<ContainerCore> container = CreateContainer(client, CreateDefinition(PartitionKeyDefinitionVersion.V2));
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            JsonFeedRange original = new JsonFeedRange(FeedRangeEpk.FullRange.ToJsonString(), cancellation.Cancel);
            FeedRange[] source = { original };
            Assert.ThrowsException<OperationCanceledException>(
                () => container.Object.CreateFeedRangeSnapshot(source, cancellation.Token));
            int serializations = original.Serializations;
            Assert.ThrowsException<OperationCanceledException>(
                () => container.Object.CreateFeedRangeSnapshot(source, cancellation.Token));
            Assert.AreEqual(serializations, original.Serializations, "Pre-canceled creation must not read range representations.");
            FeedRangeSnapshot snapshot = container.Object.CreateFeedRangeSnapshot(source);
            Assert.AreSame(original, await container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")));

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(
                () => container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value"), cancellation.Token));
            container.Verify(c => c.GetPartitionKeyDefinitionAsync(cancellation.Token), Times.Never);
        }

        [TestMethod]
        public async Task LookupPropagatesMetadataCancellationAndConvertsDocumentClientErrors()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<ContainerCore> container = CreateContainer(client, CreateDefinition(PartitionKeyDefinitionVersion.V2));
            FeedRangeSnapshot snapshot = container.Object.CreateFeedRangeSnapshot(new[] { FeedRangeEpk.FullRange });
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            container.Setup(c => c.GetPartitionKeyDefinitionAsync(cancellation.Token))
                .Returns(() =>
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<PartitionKeyDefinition>(cancellation.Token);
                });
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(
                () => container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value"), cancellation.Token));
            container.Setup(c => c.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DocumentClientException("Metadata unavailable", innerException: null, statusCode: HttpStatusCode.ServiceUnavailable));
            CosmosException exception = await Assert.ThrowsExceptionAsync<CosmosException>(
                () => container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")));
            Assert.AreEqual(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        }

        [TestMethod]
        public async Task LookupValidatesNullForeignAndEmptySnapshots()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<ContainerCore> container = CreateContainer(client, CreateDefinition(PartitionKeyDefinitionVersion.V2));
            await Assert.ThrowsExceptionAsync<ArgumentNullException>(
                () => container.Object.TryGetFeedRangeForPartitionKeyAsync(null, new PartitionKey("value")));
            Mock<ContainerCore> other = CreateContainer(client, CreateDefinition(PartitionKeyDefinitionVersion.V2), containerId: "other");
            FeedRangeSnapshot foreign = other.Object.CreateFeedRangeSnapshot(new[] { FeedRangeEpk.FullRange });
            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => container.Object.TryGetFeedRangeForPartitionKeyAsync(foreign, new PartitionKey("value")));
            Assert.AreEqual("snapshot", exception.ParamName);
            container.Verify(c => c.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()), Times.Never);
            other.Verify(c => c.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.IsNull(await container.Object.TryGetFeedRangeForPartitionKeyAsync(container.Object.CreateFeedRangeSnapshot(Array.Empty<FeedRange>()), new PartitionKey("value")));
            container.Verify(c => c.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task SnapshotSupportsSameResourceHandlesButRejectsOtherClientsAndResources()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            using CosmosClient otherClient = MockCosmosUtil.CreateMockCosmosClient();
            PartitionKeyDefinition definition = CreateDefinition(PartitionKeyDefinitionVersion.V2);
            Mock<ContainerCore> creator = CreateContainer(client, definition);
            FeedRange original = FeedRangeEpk.FullRange;
            FeedRangeSnapshot snapshot = creator.Object.CreateFeedRangeSnapshot(new[] { original });
            Mock<ContainerCore> sameResource = CreateContainer(client, definition);
            Assert.AreSame(original, await sameResource.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")));

            Mock<ContainerCore>[] rejected =
            {
                CreateContainer(otherClient, definition),
                CreateContainer(client, definition, databaseId: "other"),
                CreateContainer(client, definition, containerId: "other"),
                CreateContainer(client, definition, databaseId: "Database"),
                CreateContainer(client, definition, containerId: "Container"),
            };
            foreach (Mock<ContainerCore> container in rejected)
            {
                ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                    () => container.Object.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")));
                Assert.AreEqual("snapshot", exception.ParamName);
                container.Verify(c => c.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()), Times.Never);
            }
        }

        [TestMethod]
        public void CreationDoesNotRequestMetadataOrRetainFailedConstruction()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            Mock<ContainerCore> container = CreateContainer(client, CreateDefinition(PartitionKeyDefinitionVersion.V2));
            JsonFeedRange range = new JsonFeedRange("not-json");
            Assert.ThrowsException<ArgumentException>(() => container.Object.CreateFeedRangeSnapshot(new[] { range }));
            range.Json = FeedRangeEpk.FullRange.ToJsonString();
            Assert.IsNotNull(container.Object.CreateFeedRangeSnapshot(new[] { range }));
            container.Verify(c => c.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>()), Times.Never);
            container.Verify(c => c.GetNonePartitionKeyValueAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Never);
            container.Verify(c => c.GetRoutingMapAsync(It.IsAny<CancellationToken>()), Times.Never);
            container.Verify(c => c.GetCachedRIDAsync(It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [TestMethod]
        public async Task PublicApiUsesMetadataWithoutRequestingCurrentRoutingRanges()
        {
            Mock<IHttpHandler> httpHandler = new Mock<IHttpHandler>();
            Uri endpoint = MockSetupsHelper.SetupSingleRegionAccount(
                "ownership-account", Microsoft.Azure.Cosmos.ConsistencyLevel.Session, httpHandler, out string regionEndpoint);
            const string rid = "ccZ1ANCszwk=";
            MockSetupsHelper.SetupContainerProperties(httpHandler, regionEndpoint, "database", "container", rid);
            ResourceId resourceId = ResourceId.Parse(rid);
            Uri routingUri = new Uri($"{regionEndpoint}/dbs/{resourceId.DatabaseId}/colls/{resourceId.DocumentCollectionId}/pkranges");
            httpHandler.Setup(handler => handler.SendAsync(
                    It.Is<HttpRequestMessage>(request => request.RequestUri == routingUri), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            CosmosClientOptions options = new CosmosClientOptions
            {
                HttpClientFactory = () => new HttpClient(new HttpHandlerHelper(httpHandler.Object)),
            };
            using CosmosClient client = new CosmosClient(endpoint.ToString(), MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey, options);
            Container container = client.GetContainer("database", "container");
            FeedRange original = new JsonFeedRange(FeedRangeEpk.FullRange.ToJsonString());
            FeedRangeSnapshot snapshot = container.CreateFeedRangeSnapshot(new[] { original });
            httpHandler.Verify(handler => handler.SendAsync(
                It.Is<HttpRequestMessage>(request => request.RequestUri.AbsolutePath != "/"), It.IsAny<CancellationToken>()), Times.Never);
            Container other = client.GetContainer("database", "container");
            Assert.AreNotSame(container, other);
            Assert.AreSame(original, await other.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")));
            Assert.AreSame(original, await container.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("value")));
            Assert.AreSame(original, await container.TryGetFeedRangeForPartitionKeyAsync(snapshot, default));
            Assert.AreSame(original, await container.TryGetFeedRangeForPartitionKeyAsync(snapshot, PartitionKey.None));
            httpHandler.Verify(handler => handler.SendAsync(
                It.Is<HttpRequestMessage>(request => request.RequestUri == routingUri), It.IsAny<CancellationToken>()), Times.Never);
        }

        private static FeedRange CreateRange(string min, string max, bool maxInclusive = false)
        {
            return new FeedRangeEpk(new Range<string>(min, max, true, maxInclusive));
        }

        private static PartitionKeyDefinition CreateDefinition(PartitionKeyDefinitionVersion version, params string[] paths)
        {
            return new PartitionKeyDefinition
            {
                Kind = PartitionKind.Hash,
                Version = version,
                Paths = new Collection<string>(paths.Length == 0 ? new[] { "/pk" } : paths),
            };
        }

        private static Mock<ContainerCore> CreateContainer(
            CosmosClient client,
            PartitionKeyDefinition definition,
            string databaseId = "database",
            string containerId = "container")
        {
            ContainerInternal original = (ContainerInternal)client.GetContainer(databaseId, containerId);
            Mock<ContainerCore> container = new Mock<ContainerCore>(original.ClientContext, (DatabaseInternal)original.Database, original.Id, null)
            {
                CallBase = true,
            };
            container.Setup(c => c.GetPartitionKeyDefinitionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(definition);
            container.Setup(c => c.GetNonePartitionKeyValueAsync(It.IsAny<ITrace>(), It.IsAny<CancellationToken>())).ReturnsAsync(PartitionKeyInternal.Undefined);
            return container;
        }

        private sealed class JsonFeedRange : FeedRange
        {
            private readonly Action onSerialize;
            private int serializations;

            public JsonFeedRange(string json, Action onSerialize = null)
            {
                this.Json = json;
                this.onSerialize = onSerialize;
            }

            public int Serializations => this.serializations;

            public string Json { get; set; }

            public override string ToJsonString()
            {
                Interlocked.Increment(ref this.serializations);
                this.onSerialize?.Invoke();
                return this.Json;
            }
        }

        private sealed class CountingSnapshot : IReadOnlyList<FeedRange>
        {
            private readonly FeedRange[] ranges;
            private int reads;

            public CountingSnapshot(FeedRange[] ranges)
            {
                this.ranges = ranges;
            }

            public int Reads => this.reads;

            public int Count => this.ranges.Length;

            public FeedRange this[int index]
            {
                get
                {
                    Interlocked.Increment(ref this.reads);
                    return this.ranges[index];
                }
            }

            public IEnumerator<FeedRange> GetEnumerator() => ((IEnumerable<FeedRange>)this.ranges).GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
        }
    }
}
#endif
