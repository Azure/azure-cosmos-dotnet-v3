//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System.Net.Http;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Net;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Telemetry;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.Azure.Documents.Routing;
    using Moq;
    using static Microsoft.Azure.Cosmos.Routing.PartitionRoutingHelper;

    internal class MockCosmosUtil
    {
        public static readonly CosmosSerializerCore Serializer = new CosmosSerializerCore();
        public static readonly string RandomInvalidCorrectlyFormatedAuthKey = "CV60UDtH10CFKR0GxBl/Wg==";

        public static CosmosClient CreateMockCosmosClient(
            Action<CosmosClientBuilder> customizeClientBuilder = null,
            Cosmos.ConsistencyLevel? accountConsistencyLevel = null,
            bool enableTelemetry = false)
        {
            ConnectionPolicy policy = new ConnectionPolicy
            {
                CosmosClientTelemetryOptions = new CosmosClientTelemetryOptions
                {
                    DisableSendingMetricsToService = !enableTelemetry
                }
            };

            DocumentClient documentClient = accountConsistencyLevel.HasValue ? new MockDocumentClient(accountConsistencyLevel.Value, policy) : new MockDocumentClient(policy);
            CosmosClientBuilder cosmosClientBuilder = new CosmosClientBuilder("http://localhost", MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey);

            customizeClientBuilder?.Invoke(cosmosClientBuilder);
            return cosmosClientBuilder.Build(documentClient);
        }

        public static Mock<ContainerInternal> CreateMockContainer(
            string dbName = "myDb",
            string containerName = "myContainer")
        {
            string link = $"/dbs/{dbName}/colls/{containerName}";
            Mock<ContainerInternal> mockContainer = new Mock<ContainerInternal>();
            mockContainer.Setup(x => x.LinkUri).Returns(link);
            return mockContainer;
        }

        public static Mock<DatabaseInternal> CreateMockDatabase(string dbName = "myDb")
        {
            string link = $"/dbs/{dbName}";
            Mock<DatabaseInternal> mockDB = new Mock<DatabaseInternal>();
            mockDB.Setup(x => x.LinkUri).Returns(link);
            mockDB.Setup(x => x.GetRIDAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult(dbName));
            return mockDB;
        }

        public static CosmosClientOptions GetDefaultConfiguration()
        {
            return new CosmosClientOptions();
        }

        public static CosmosHttpClient CreateCosmosHttpClient(
            Func<HttpClient> httpClient,
            DocumentClientEventSource eventSource = null)
        {
            eventSource ??= DocumentClientEventSource.Instance;

            ConnectionPolicy connectionPolicy = new ConnectionPolicy()
            {
                HttpClientFactory = httpClient
            };

            return CosmosHttpClientCore.CreateWithConnectionPolicy(
                apiType: default,
                eventSource: eventSource,
                connectionPolicy: connectionPolicy,
                httpMessageHandler: null,
                sendingRequestEventArgs: null,
                receivedResponseEventArgs: null);
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
                It.IsAny<ITrace>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).Returns(Task.FromResult(new ResolvedRangeInfo(new PartitionKeyRange { Id = partitionRangeKeyId }, new List<CompositeContinuationToken>())));
            partitionRoutingHelperMock.Setup(m => m.TryAddPartitionKeyRangeToContinuationTokenAsync(
                It.IsAny<INameValueCollection>(),
                It.IsAny<List<Range<string>>>(),
                It.IsAny<IRoutingMapProvider>(),
                It.IsAny<string>(),
                It.IsAny<ResolvedRangeInfo>(),
                It.IsAny<ITrace>(),
                It.IsAny<RntbdConstants.RntdbEnumerationDirection>()
            )).Returns(Task.FromResult(true));
            return partitionRoutingHelperMock;
        }

        public static Task<HttpResponseMessage> CreateHttpResponseOfAddresses(List<string> physicalUris)
        {
            List<Address> addresses = new List<Address>();
            for (int i = 0; i < physicalUris.Count; i++)
            {
                addresses.Add(new Address()
                {
                    IsPrimary = i == 0,
                    PhysicalUri = physicalUris[i],
                    Protocol = RuntimeConstants.Protocols.RNTBD,
                    PartitionKeyRangeId = "YxM9ANCZIwABAAAAAAAAAA=="
                });
            };

            FeedResource<Address> addressFeedResource = new FeedResource<Address>()
            {
                Id = "YxM9ANCZIwABAAAAAAAAAA==",
                SelfLink = "dbs/YxM9AA==/colls/YxM9ANCZIwA=/docs/YxM9ANCZIwABAAAAAAAAAA==/",
                Timestamp = DateTime.Now,
                InnerCollection = new Collection<Address>(addresses),
            };

            StringBuilder feedResourceString = new StringBuilder();
            addressFeedResource.SaveTo(feedResourceString);

            StringContent content = new StringContent(feedResourceString.ToString());
            HttpResponseMessage responseMessage = new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = content,
            };

            return Task.FromResult(responseMessage);
        }
    }
}