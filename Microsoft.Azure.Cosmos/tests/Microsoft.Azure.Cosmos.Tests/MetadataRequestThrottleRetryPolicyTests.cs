//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Tracing;
    using System.Net;

    /// <summary>
    /// Unit tests for <see cref="MetadataRequestThrottleRetryPolicy"/>.
    /// </summary>
    [TestClass]
    public class MetadataRequestThrottleRetryPolicyTests
    {
        [TestMethod]
        [Owner("dkunda")]
        [DataRow(true, true, DisplayName = "Test when a response message with a valid substatus code was used.")]
        [DataRow(false, true, DisplayName = "Test when an exception was thrown with a valid substatus code.")]
        [DataRow(true, false, DisplayName = "Test when a response message with an invalid substatus code was used.")]
        [DataRow(false, false, DisplayName = "Test when an exception was thrown with an invalid substatus code.")]
        public async Task ShouldRetryAsync_WithValidAndInvalidSubStatusCodes_ShouldMarkReadEndpointUnavailableOrSkipMarkingEndpoint(
            bool useResponseMessage,
            bool isValidSubStatusCode)
        {
            // Arrange.
            ShouldRetryResult retryResult;
            string collectionRid = "test-collection";
            Uri primaryServiceEndpoint = new ("https://default-endpoint.net/");
            Uri serviceEndpointMarkedUnavailableForRead = null;

            Documents.Collections.INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();

            headers.Set(HttpConstants.HttpHeaders.PageSize, "10");
            headers.Set(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.ReadFeed,
                    collectionRid,
                    Documents.ResourceType.PartitionKeyRange,
                    AuthorizationTokenType.PrimaryMasterKey,
                    headers);

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = new ();
            mockedGlobalEndpointManager
                .Setup(gem => gem.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                .Returns(primaryServiceEndpoint);
            mockedGlobalEndpointManager
                .Setup(gem => gem.MarkEndpointUnavailableForRead(primaryServiceEndpoint))
                .Callback((Uri endpoint) => 
                {
                    serviceEndpointMarkedUnavailableForRead = endpoint;
                });


            MetadataRequestThrottleRetryPolicy policy = new (mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            // Act.
            if (useResponseMessage)
            {
                Headers responseHeaders = new()
                {
                    SubStatusCode = isValidSubStatusCode
                    ? SubStatusCodes.TransportGenerated503
                    : SubStatusCodes.BWTermCountLimitExceeded
                };

                ResponseMessage responseMessage = new(
                    statusCode: HttpStatusCode.ServiceUnavailable,
                    requestMessage: null,
                    headers: responseHeaders,
                    cosmosException: null,
                    trace: NoOpTrace.Singleton);

                retryResult = await policy.ShouldRetryAsync(responseMessage, default);
            }
            else
            {
                CosmosException exception = CosmosExceptionFactory.CreateServiceUnavailableException(
                                                message: "Service Unavailable at the moment.",
                                                headers: new Headers()
                                                {
                                                    ActivityId = System.Diagnostics.Trace.CorrelationManager.ActivityId.ToString(),
                                                    SubStatusCode = isValidSubStatusCode
                                                        ? SubStatusCodes.TransportGenerated503
                                                        : SubStatusCodes.BWTermCountLimitExceeded
                                                },
                                                trace: NoOpTrace.Singleton,
                                                innerException: null);


                retryResult = await policy.ShouldRetryAsync(exception, default);
            }

            // Assert.
            Assert.IsNotNull(retryResult);
            Assert.AreEqual(false, retryResult.ShouldRetry, "ResourceThrottleRetryPolicy should return false since the status code does not indicate the request was throttled.");
            if (isValidSubStatusCode)
            {
                Assert.IsNotNull(serviceEndpointMarkedUnavailableForRead);
                Assert.AreEqual(primaryServiceEndpoint, serviceEndpointMarkedUnavailableForRead, "Both the primary endpoint and the endpoint that was marked unavailable should match.");
            }
            else
            {
                Assert.IsNull(serviceEndpointMarkedUnavailableForRead);
            }
        }
    }
}
