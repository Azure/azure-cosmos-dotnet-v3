//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Client.Core.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Moq;
    using static Microsoft.Azure.Cosmos.Routing.PartitionRoutingHelper;

    internal class MockCosmosUtil
    {
        public static CosmosClient CreateMockCosmosClient(Action<CosmosClientBuilder> customizeClientBuilder = null)
        {
            DocumentClient documentClient = new MockDocumentClient();
            
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Guid.NewGuid().ToString());
            if (customizeClientBuilder != null)
            {
                customizeClientBuilder(cosmosClientBuilder);
            }

            return cosmosClientBuilder.Build(documentClient);
        }

        public static Mock<CosmosContainerCore> CreateMockContainer(
            string dbName = "myDb",
            string containerName = "myContainer")
        {
            Uri link = new Uri($"/dbs/{dbName}/colls/{containerName}" , UriKind.Relative);
            Mock<CosmosContainerCore> mockContainer = new Mock<CosmosContainerCore>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            return mockContainer;
        }

        public static Mock<CosmosDatabaseCore> CreateMockDatabase(string dbName = "myDb")
        {
            Uri link = new Uri($"/dbs/{dbName}", UriKind.Relative);
            Mock<CosmosDatabaseCore> mockDB = new Mock<CosmosDatabaseCore>();
            mockDB.Setup(x => x.LinkUri).Returns(link);
            mockDB.Setup(x => x.GetRIDAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(dbName));
            return mockDB;
        }

        public static CosmosClientOptions GetDefaultConfiguration()
        {
            return new CosmosClientOptions(endPoint: "http://localhost", accountKey: "MockedCosmosClientAccountKeyDummyValue");
        }

        public static Mock<PartitionRoutingHelper> GetPartitionRoutingHelperMock(string partitionRangeKeyId)
        {
            Mock<PartitionRoutingHelper> partitionRoutingHelperMock = new Mock<PartitionRoutingHelper>();
            partitionRoutingHelperMock.Setup(
                m => m.ExtractPartitionKeyRangeFromContinuationToken(It.IsAny<INameValueCollection>(), out It.Ref<List<CompositeContinuationToken>>.IsAny
            )).Returns(new Range<string>("A", "B", true, false));
            partitionRoutingHelperMock.Setup(m => m.TryGetTargetRangeFromContinuationTokenRangeAsync(
                It.IsAny<IReadOnlyList<Range<string>>>(),
                It.IsAny<IRoutingMapProvider>(),
                It.IsAny<string>(),
                It.IsAny<Range<string>>(),
                It.IsAny<List<CompositeContinuationToken>>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).Returns(Task.FromResult(new ResolvedRangeInfo(new PartitionKeyRange { Id = partitionRangeKeyId }, new List<CompositeContinuationToken>())));
            partitionRoutingHelperMock.Setup(m => m.TryAddPartitionKeyRangeToContinuationTokenAsync(
                It.IsAny<INameValueCollection>(),
                It.IsAny<List<Range<string>>>(),
                It.IsAny<IRoutingMapProvider>(),
                It.IsAny<string>(),
                It.IsAny<ResolvedRangeInfo>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).Returns(Task.FromResult(true));
            return partitionRoutingHelperMock;
        }
    }
}
