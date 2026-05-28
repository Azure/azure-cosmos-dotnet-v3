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
        [DataRow(true, DisplayName = "Test when a response message.")]
        [DataRow(false, DisplayName = "Test when an exception was thrown.")]
        public async Task ShouldRetryAsync_WithValidAndInvalidSubStatusCodes_ShouldIncrementLocationIndexOrSkip(
            bool useResponseMessage)
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
                .Returns(routedServiceEndpoint);

            MetadataRequestThrottleRetryPolicy policy = new(mockedGlobalEndpointManager.Object, 0);
            policy.OnBeforeSendRequest(request);

            Assert.AreEqual(primaryServiceEndpoint, request.RequestContext.LocationEndpointToRoute);

            // Act.
            if (useResponseMessage)
            {
                Headers responseHeaders = new()
                {
                    SubStatusCode = SubStatusCodes.TransportGenerated503
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
                                                    SubStatusCode = SubStatusCodes.TransportGenerated503
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
            Assert.AreEqual(true, retryResult.ShouldRetry, "MetadataRequestThrottleRetryPolicy should return true since the sub status code indicates to retry the request in the next preferred read region.");
            Assert.AreEqual(1, retryContext.RetryLocationIndex, "Indicates that the retry location index was incremented.");
            Assert.AreEqual(routedServiceEndpoint, request.RequestContext.LocationEndpointToRoute);
        }
    }
}