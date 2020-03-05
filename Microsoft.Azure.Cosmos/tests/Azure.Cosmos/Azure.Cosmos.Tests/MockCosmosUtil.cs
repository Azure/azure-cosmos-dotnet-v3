//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Cosmos.Fluent;
    using Azure.Cosmos.Serialization;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Query;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Moq;
    using static Microsoft.Azure.Cosmos.Routing.PartitionRoutingHelper;

    internal class MockCosmosUtil
    {
        public static readonly CosmosSerializer Serializer = CosmosTextJsonSerializer.CreatePropertiesSerializer();

        public static CosmosClient CreateMockCosmosClient(
            Action<CosmosClientBuilder> customizeClientBuilder = null,
            Cosmos.ConsistencyLevel? accountConsistencyLevel = null)
        {
            DocumentClient documentClient;
            if (accountConsistencyLevel.HasValue)
            {
                documentClient = new MockDocumentClient(accountConsistencyLevel.Value);
            }
            else
            {
                documentClient = new MockDocumentClient();
            }
            
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", Guid.NewGuid().ToString());
            if (customizeClientBuilder != null)
            {
                customizeClientBuilder(cosmosClientBuilder);
            }

            return cosmosClientBuilder.Build(documentClient);
        }

        public static Mock<ContainerCore> CreateMockContainer(
            string dbName = "myDb",
            string containerName = "myContainer")
        {
            Uri link = new Uri($"/dbs/{dbName}/colls/{containerName}" , UriKind.Relative);
            Mock<ContainerCore> mockContainer = new Mock<ContainerCore>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            return mockContainer;
        }

        public static Mock<DatabaseCore> CreateMockDatabase(string dbName = "myDb")
        {
            Uri link = new Uri($"/dbs/{dbName}", UriKind.Relative);
            Mock<DatabaseCore> mockDB = new Mock<DatabaseCore>();
            mockDB.Setup(x => x.LinkUri).Returns(link);
            mockDB.Setup(x => x.GetRIDAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(dbName));
            return mockDB;
        }

        public static CosmosClientOptions GetDefaultConfiguration()
        {
            return new CosmosClientOptions();
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
