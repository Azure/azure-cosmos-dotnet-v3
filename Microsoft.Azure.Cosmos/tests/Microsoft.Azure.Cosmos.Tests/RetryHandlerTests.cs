//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure;
    using Microsoft.Azure.Cosmos.Handlers;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class RetryHandlerTests
    {
        private static readonly Uri TestUri = new Uri("https://dummy.documents.azure.com:443/dbs");
        [TestMethod]
        public async Task ValidateQueryPlanDoesNotThrowExceptionForOverlappingRanges()
        {
            await this.ValidateOverlappingRangesBehaviorAsync(
                operationType: OperationType.QueryPlan,
                shouldThrowGoneException: false);
        }

        [TestMethod]
        public async Task ValidateQueryThrowsGoneExceptionForOverlappingRanges()
        {
            await this.ValidateOverlappingRangesBehaviorAsync(
                operationType: OperationType.Query,
                shouldThrowGoneException: true);
        }

        private async Task ValidateOverlappingRangesBehaviorAsync(
            OperationType operationType,
            bool shouldThrowGoneException)
        {
            // Create overlapping ranges for the test
            List<PartitionKeyRange> overlappingRanges = new List<PartitionKeyRange>
            {
                new PartitionKeyRange { Id = "0", MinInclusive = "0D4DC2CD8F49C65A8E0C5306B61B4343", MaxExclusive = "0DCEB8CE51C6BFE84F4BD9409F69B9BB2164DEBD78C50C850E0C1E3E3F0579ED" },
                new PartitionKeyRange { Id = "1", MinInclusive = "0DCEB8CE51C6BFE84F4BD9409F69B9BB2164DEBD78C50C850E0C1E3E3F0579ED", MaxExclusive = "1080F600C27CF98DC13F8639E94E7676" }
            };

            // Create a custom document client with our TestPartitionKeyRangeCache
            var testPartitionKeyRangeCache = new TestPartitionKeyRangeCache(overlappingRanges);
            var customDocClient = new CustomMockDocumentClient(testPartitionKeyRangeCache);

            // Create CosmosClient with our custom document client
            using CosmosClient client = new CosmosClient(
                "https://localhost:8081",
                MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey,
                new CosmosClientOptions(),
                customDocClient);

            // Create mock container 
            Mock<ContainerInternal> containerMock = MockCosmosUtil.CreateMockContainer("testDb", "testColl");

            // Setup container properties
            ContainerProperties containerProps = new ContainerProperties("testColl", "/pk");
            var resourceIdProperty = typeof(ContainerProperties).GetProperty(
                "ResourceId",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            resourceIdProperty.SetValue(containerProps, "testCollRid");

            // Set up additional mocks as needed
            containerMock.Setup(c => c.GetCachedContainerPropertiesAsync(
                It.IsAny<bool>(), It.IsAny<ITrace>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProps);

            Mock<Cosmos.Database> databaseMock = new Mock<Cosmos.Database>();
            databaseMock.Setup(d => d.Id).Returns("testDb");
            containerMock.Setup(c => c.Database).Returns(databaseMock.Object);

            // FeedRangeEpk for the test - use a range that overlaps both partition key ranges
            FeedRangeEpk feedRange = new FeedRangeEpk(new Documents.Routing.Range<string>(
                "0DCEB8CE51C6BFE84F4BD9409F69B9BB",
                "0DCEB8CE51C6BFE84F4BD9409F69B9BBFF", 
                true, false));

            RequestInvokerHandler invoker = new RequestInvokerHandler(client, null, null, null)
            {
                InnerHandler = new TestHandler((request, token) => TestHandler.ReturnSuccess())
            };

            // Act
            ResponseMessage response = await invoker.SendAsync(
                "dbs/testDb/colls/testColl",
                ResourceType.Document,
                operationType,
                null,
                containerMock.Object,
                feedRange,
                null,
                null,
                NoOpTrace.Singleton,
                CancellationToken.None);

            // Assert
            Assert.IsNotNull(response, "Response should not be null.");

            if (shouldThrowGoneException)
            {
                Assert.IsFalse(response.IsSuccessStatusCode, "Expected a failure status code for Query operation.");
                Assert.AreEqual(HttpStatusCode.Gone, response.StatusCode, "Expected a 410 Gone status code.");
                Assert.AreEqual((int)SubStatusCodes.PartitionKeyRangeGone, (int)response.Headers.SubStatusCode, "Expected PartitionKeyRangeGone sub-status code.");
            }
            else
            {
                Assert.IsTrue(response.IsSuccessStatusCode, $"Expected a successful status code, but got {response.StatusCode}.");
            }
        }

        // Custom MockDocumentClient that allows injecting our TestPartitionKeyRangeCache
        private class CustomMockDocumentClient : MockDocumentClient
        {
            private readonly TestPartitionKeyRangeCache testPartitionKeyRangeCache;

            public CustomMockDocumentClient(TestPartitionKeyRangeCache testPartitionKeyRangeCache)
                : base(new ConnectionPolicy())
            {
                this.testPartitionKeyRangeCache = testPartitionKeyRangeCache;
            }

            internal override Task<PartitionKeyRangeCache> GetPartitionKeyRangeCacheAsync(ITrace trace)
            {
                return Task.FromResult<PartitionKeyRangeCache>(this.testPartitionKeyRangeCache);
            }
        }

        private class TestPartitionKeyRangeCache : PartitionKeyRangeCache
        {
            private readonly IReadOnlyList<PartitionKeyRange> overlappingRanges;

            public TestPartitionKeyRangeCache(IReadOnlyList<PartitionKeyRange> overlappingRanges)
                : base(null, null, null, null) // Pass nulls or mocks as needed for base constructor
            {
                this.overlappingRanges = overlappingRanges;
            }

            public override Task<IReadOnlyList<PartitionKeyRange>> TryGetOverlappingRangesAsync(
                string collectionRid,
                Documents.Routing.Range<string> range,
                ITrace trace,
                PartitionKeyDefinition partitionKeyDefinition,
                bool forceRefresh)
            {
                return Task.FromResult(this.overlappingRanges);
            }
        }


        [TestMethod]
        public async Task RetryHandlerDoesNotRetryOnSuccess()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RetryHandler retryHandler = new RetryHandler(client);
            int handlerCalls = 0;
            int expectedHandlerCalls = 1;
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(
                client,
                requestedClientConsistencyLevel: null,
                requestedClientPriorityLevel: null,
                requestedClientThroughputBucket: null)
            {
                InnerHandler = retryHandler
            };
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Delete, RetryHandlerTests.TestUri);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task RetryHandlerRetriesOn429()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RetryHandler retryHandler = new RetryHandler(client);
            int handlerCalls = 0;
            int expectedHandlerCalls = 2;
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                if (handlerCalls == 0)
                {
                    handlerCalls++;
                    return TestHandler.ReturnStatusCode((HttpStatusCode)StatusCodes.TooManyRequests);
                }

                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(
                client,
                requestedClientConsistencyLevel: null,
                requestedClientPriorityLevel: null,
                requestedClientThroughputBucket: null)
            {
                InnerHandler = retryHandler
            };
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Delete, RetryHandlerTests.TestUri);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        [ExpectedException(typeof(Exception))]
        public async Task RetryHandlerDoesNotRetryOnException()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            RetryHandler retryHandler = new RetryHandler(client);
            int handlerCalls = 0;
            int expectedHandlerCalls = 2;
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                handlerCalls++;
                if (handlerCalls == expectedHandlerCalls)
                {
                    Assert.Fail("Should not retry on exception.");
                }

                throw new Exception("You shall not retry.");
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(
                client,
                requestedClientConsistencyLevel: null,
                requestedClientPriorityLevel: null,
                requestedClientThroughputBucket: null)
            {
                InnerHandler = retryHandler
            };
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());
        }

        [TestMethod]
        public async Task RetryHandlerHttpClientExceptionRefreshesLocations()
        {
            using DocumentClient dc = new MockDocumentClient(RetryHandlerTests.TestUri, MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey);
            using CosmosClient client = new CosmosClient(
                RetryHandlerTests.TestUri.OriginalString,
                MockCosmosUtil.RandomInvalidCorrectlyFormatedAuthKey,
                new CosmosClientOptions(),
                dc);

            Mock<IDocumentClientRetryPolicy> mockClientRetryPolicy = new Mock<IDocumentClientRetryPolicy>();

            mockClientRetryPolicy.Setup(m => m.ShouldRetryAsync(It.IsAny<Exception>(), It.IsAny<CancellationToken>()))
                .Returns<Exception, CancellationToken>((ex, tooken) => Task.FromResult(ShouldRetryResult.RetryAfter(TimeSpan.FromMilliseconds(1))));

            Mock<IRetryPolicyFactory> mockRetryPolicy = new Mock<IRetryPolicyFactory>();
            mockRetryPolicy.Setup(m => m.GetRequestPolicy())
                .Returns(() => mockClientRetryPolicy.Object);

            RetryHandler retryHandler = new RetryHandler(client);
            int handlerCalls = 0;
            int expectedHandlerCalls = 2;
            TestHandler testHandler = new TestHandler((request, response) =>
            {
                handlerCalls++;
                if (handlerCalls == expectedHandlerCalls)
                {
                    return TestHandler.ReturnSuccess();
                }

                throw new HttpRequestException("DNS or some other network issue");
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(
                client,
                requestedClientConsistencyLevel: null,
                requestedClientPriorityLevel: null,
                requestedClientThroughputBucket: null)
            {
                InnerHandler = retryHandler
            };
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new System.Uri("https://dummy.documents.azure.com:443/dbs"));
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task RetryHandlerNoRetryOnAuthError()
        {
            await this.RetryHandlerDontRetryOnStatusCode(HttpStatusCode.Unauthorized);
        }

        [TestMethod]
        public async Task RetryHandlerNoRetryOnWriteForbidden()
        {
            await this.RetryHandlerDontRetryOnStatusCode(HttpStatusCode.Forbidden, SubStatusCodes.WriteForbidden);
        }

        [TestMethod]
        public async Task RetryHandlerNoRetryOnSessionNotAvailable()
        {
            await this.RetryHandlerDontRetryOnStatusCode(HttpStatusCode.NotFound, SubStatusCodes.ReadSessionNotAvailable);
        }

        [TestMethod]
        public async Task RetryHandlerNoRetryOnDatabaseAccountNotFound()
        {
            await this.RetryHandlerDontRetryOnStatusCode(HttpStatusCode.Forbidden, SubStatusCodes.DatabaseAccountNotFound);
        }

        private async Task RetryHandlerDontRetryOnStatusCode(
                HttpStatusCode statusCode,
                SubStatusCodes subStatusCode = SubStatusCodes.Unknown)
        {
            int handlerCalls = 0;
            TestHandler testHandler = new TestHandler((request, response) =>
            {
                handlerCalls++;

                if (handlerCalls == 0)
                {
                    return TestHandler.ReturnStatusCode(statusCode, subStatusCode);
                }

                return TestHandler.ReturnSuccess();
            });

            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();
            RetryHandler retryHandler = new RetryHandler(client)
            {
                InnerHandler = testHandler
            };

            RequestInvokerHandler invoker = new RequestInvokerHandler(
                client,
                requestedClientConsistencyLevel: null,
                requestedClientPriorityLevel: null,
                requestedClientThroughputBucket: null)
            {
                InnerHandler = retryHandler
            };
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Delete, RetryHandlerTests.TestUri);
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[]");
            requestMessage.ResourceType = ResourceType.Document;
            requestMessage.OperationType = OperationType.Read;
            await invoker.SendAsync(requestMessage, new CancellationToken());

            int expectedHandlerCalls = 1;
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task InvalidPartitionExceptionRetryHandlerDoesNotRetryOnSuccess()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            NamedCacheRetryHandler retryHandler = new NamedCacheRetryHandler();
            int handlerCalls = 0;
            int expectedHandlerCalls = 1;
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(
                client,
                requestedClientConsistencyLevel: null,
                requestedClientPriorityLevel: null,
                requestedClientThroughputBucket: null)
            {
                InnerHandler = retryHandler
            };
            RequestMessage requestMessage = new RequestMessage(
                HttpMethod.Get,
                new Uri("https://dummy.documents.azure.com:443/dbs"));
            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }

        [TestMethod]
        public async Task InvalidPartitionExceptionRetryHandlerDoesNotRetryOn410()
        {
            using CosmosClient client = MockCosmosUtil.CreateMockCosmosClient();

            NamedCacheRetryHandler retryHandler = new NamedCacheRetryHandler();
            int handlerCalls = 0;
            int expectedHandlerCalls = 2;
            TestHandler testHandler = new TestHandler((request, cancellationToken) =>
            {
                request.OnBeforeSendRequestActions(request.ToDocumentServiceRequest());
                if (handlerCalls == 0)
                {
                    handlerCalls++;
                    return TestHandler.ReturnStatusCode((HttpStatusCode)StatusCodes.Gone, SubStatusCodes.NameCacheIsStale);
                }

                handlerCalls++;
                return TestHandler.ReturnSuccess();
            });

            retryHandler.InnerHandler = testHandler;
            RequestInvokerHandler invoker = new RequestInvokerHandler(
                client,
                requestedClientConsistencyLevel: null,
                requestedClientPriorityLevel: null,
                requestedClientThroughputBucket: null)
            {
                InnerHandler = retryHandler
            };
            RequestMessage requestMessage = new RequestMessage(HttpMethod.Get, new Uri("https://dummy.documents.azure.com:443/dbs"));

            await invoker.SendAsync(requestMessage, new CancellationToken());
            Assert.AreEqual(expectedHandlerCalls, handlerCalls);
        }
    }
}