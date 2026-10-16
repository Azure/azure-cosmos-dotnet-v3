//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if PREVIEW
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TryGetFeedRangeForPartitionKeyAsyncTests
    {
        private CosmosClient client;
        private Database database;

        [TestInitialize]
        public async Task Initialize()
        {
            this.client = TestCommon.CreateCosmosClient();
            this.database = await this.client.CreateDatabaseAsync(Guid.NewGuid().ToString());
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            try
            {
                if (this.database != null)
                {
                    await this.database.DeleteAsync();
                }
            }
            finally
            {
                this.client?.Dispose();
            }
        }

        [TestMethod]
        [DataRow(PartitionKeyDefinitionVersion.V1)]
        [DataRow(PartitionKeyDefinitionVersion.V2)]
        public async Task ResolvesKeysInCallerSnapshot(PartitionKeyDefinitionVersion version)
        {
            Container container = await this.database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), "/pk")
                {
                    PartitionKeyDefinitionVersion = version,
                },
                throughput: 20000);
            IReadOnlyList<FeedRange> ranges = (await container.GetFeedRangesAsync()).Reverse().ToArray();
            Assert.IsTrue(ranges.Count > 1);
            FeedRangeSnapshot snapshot = container.CreateFeedRangeSnapshot(ranges);

            foreach (PartitionKey key in new[] { new PartitionKey("WA"), new PartitionKey(42), new PartitionKey(true), PartitionKey.Null, default, PartitionKey.None })
            {
                FeedRange owner = await container.TryGetFeedRangeForPartitionKeyAsync(snapshot, key);
                Assert.IsNotNull(owner);
                Assert.IsTrue(ranges.Any(range => object.ReferenceEquals(range, owner)));
                Assert.AreSame(owner, await container.TryGetFeedRangeForPartitionKeyAsync(container.CreateFeedRangeSnapshot(new[] { owner }), key));
                Assert.IsNull(await container.TryGetFeedRangeForPartitionKeyAsync(container.CreateFeedRangeSnapshot(ranges.Where(range => !object.ReferenceEquals(range, owner)).ToArray()), key));
                if (!key.IsNone)
                {
                    Assert.IsTrue(await container.IsFeedRangePartOfAsync(owner, FeedRange.FromPartitionKey(key.InternalKey == null ? PartitionKey.Null : key)));
                }
            }
        }

        [TestMethod]
        public async Task ResolvesCompleteHierarchicalKeysAndRejectsPrefixes()
        {
            Container container = await this.database.CreateContainerAsync(
                new ContainerProperties(Guid.NewGuid().ToString(), new List<string> { "/city", "/state" }),
                throughput: 20000);
            IReadOnlyList<FeedRange> ranges = await container.GetFeedRangesAsync();
            FeedRangeSnapshot snapshot = container.CreateFeedRangeSnapshot(ranges);
            PartitionKey key = new PartitionKeyBuilder().Add("Redmond").Add("WA").Build();
            FeedRange owner = await container.TryGetFeedRangeForPartitionKeyAsync(snapshot, key);
            Assert.IsNotNull(owner);
            Assert.IsTrue(ranges.Any(range => object.ReferenceEquals(range, owner)));
            Assert.IsTrue(await container.IsFeedRangePartOfAsync(owner, FeedRange.FromPartitionKey(key)));
            await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => container.TryGetFeedRangeForPartitionKeyAsync(snapshot, new PartitionKey("Redmond")));
        }
    }
}
#endif
