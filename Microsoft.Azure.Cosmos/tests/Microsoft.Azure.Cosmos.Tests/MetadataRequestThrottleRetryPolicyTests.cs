//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using System;
    using System.Net;
    using System.Reflection;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.Resource.CosmosExceptions;
    using Microsoft.Azure.Cosmos.Routing;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.Azure.Documents;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;
    using static Microsoft.Azure.Cosmos.MetadataRequestThrottleRetryPolicy;

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
        public async Task ShouldRetryAsync_WithValidAndInvalidSubStatusCodes_ShouldIncrementLocationIndexOrSkip(
            bool useResponseMessage,
            bool isValidSubStatusCode)
        {
            // Arrange.
            ShouldRetryResult retryResult;
            string collectionRid = "test-collection";
            Uri primaryServiceEndpoint = new("https://default-endpoint-region1.net/");
            Uri routedServiceEndpoint = new("https://default-endpoint-region2.net/");

            Documents.Collections.INameValueCollection headers = new Documents.Collections.RequestNameValueCollection();

            headers.Set(HttpConstants.HttpHeaders.PageSize, "10");
            headers.Set(HttpConstants.HttpHeaders.A_IM, HttpConstants.A_IMHeaderValues.IncrementalFeed);

            DocumentServiceRequest request = DocumentServiceRequest.Create(
                    OperationType.ReadFeed,
                    collectionRid,
                    Documents.ResourceType.PartitionKeyRange,
                    AuthorizationTokenType.PrimaryMasterKey,
                    headers);

            Mock<IGlobalEndpointManager> mockedGlobalEndpointManager = new();
            mockedGlobalEndpointManager
                .SetupSequence(gem => gem.ResolveServiceEndpoint(It.IsAny<DocumentServiceRequest>()))
                .Returns(primaryServiceEndpoint)
                .Returns(isValidSubStatusCode ? routedServiceEndpoint : primaryServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            Assert.AreEqual(primaryServiceEndpoint, request.RequestContext.LocationEndpointToRoute);

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

            policy.OnBeforeSendRequest(request);

            // Assert.
            FieldInfo fieldInfo = policy
                .GetType()
                .GetField(
                    name: "retryContext",
                    bindingAttr: BindingFlags.Instance | BindingFlags.NonPublic);

            MetadataRetryContext retryContext = (MetadataRetryContext)fieldInfo
                .GetValue(
                    obj: policy);

            Assert.IsNotNull(retryResult);
            if (isValidSubStatusCode)
            {
                Assert.AreEqual(true, retryResult.ShouldRetry, "MetadataRequestThrottleRetryPolicy should return true since the sub status code indicates to retry the request in the next preferred read region.");
                Assert.AreEqual(1, retryContext.RetryLocationIndex, "Indicates that the retry location index was incremented.");
                Assert.AreEqual(routedServiceEndpoint, request.RequestContext.LocationEndpointToRoute);
            }
            else
            {
                Assert.AreEqual(false, retryResult.ShouldRetry, "ResourceThrottleRetryPolicy should return false since the status code does not indicate the request was throttled.");
                Assert.AreEqual(0, retryContext.RetryLocationIndex, "Indicates that the retry location index remain unchanged.");
                Assert.AreEqual(primaryServiceEndpoint, request.RequestContext.LocationEndpointToRoute);
            }
        }
    }
}