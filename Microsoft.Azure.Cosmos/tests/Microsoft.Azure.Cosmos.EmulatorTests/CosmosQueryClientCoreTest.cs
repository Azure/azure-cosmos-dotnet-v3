//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------
namespace Microsoft.Azure.Cosmos.SDK.EmulatorTests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Query.Core.QueryClient;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CosmosQueryClientCoreTest : BaseCosmosClientHelper
    {
        private ContainerInternal Container = null;
        private CosmosQueryClientCore queryClientCore = null;

        [TestInitialize]
        public async Task TestInitialize()
        {
            await base.TestInit();
            // Throughput needs to be large enough for multiple partitions
            ContainerResponse response = await this.database.CreateContainerAsync(
                Guid.NewGuid().ToString(),
                "/id",
                15000,
                cancellationToken: this.cancellationToken);
            Assert.IsNotNull(response);
            Assert.IsNotNull(response.Container);
            Assert.IsNotNull(response.Resource);
            this.Container = (ContainerInlineCore)response;
            this.queryClientCore = new CosmosQueryClientCore(this.Container.ClientContext, this.Container);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            await base.TestCleanup();
        }

        [TestMethod]
        public async Task TryGetOverlappingRangesAsyncTest()
        {
            ContainerQueryProperties containerProperties = await this.queryClientCore.GetCachedContainerQueryPropertiesAsync(
                containerLink: this.Container.LinkUri,
                partitionKey: new PartitionKey("Test"),
                cancellationToken: default);

            Assert.IsNotNull(containerProperties);
            Assert.IsNotNull(containerProperties.ResourceId);
            Assert.IsNotNull(containerProperties.EffectivePartitionKeyString);

            IReadOnlyList<Documents.PartitionKeyRange> pkRange = await this.queryClientCore.TryGetOverlappingRangesAsync(
                collectionResourceId: containerProperties.ResourceId,
                range: new Documents.Routing.Range<string>("AA", "AB", true, false),
                forceRefresh: false);

            Assert.IsNotNull(pkRange);
            Assert.AreEqual(1, pkRange.Count);

            IReadOnlyList<Documents.PartitionKeyRange> pkRangeAll = await this.queryClientCore.TryGetOverlappingRangesAsync(
                collectionResourceId: containerProperties.ResourceId,
                range: new Documents.Routing.Range<string>("00", "FF", true, false),
                forceRefresh: false);

            Assert.IsNotNull(pkRangeAll);
            Assert.IsTrue(pkRangeAll.Count > 1);
        }
    }
}
