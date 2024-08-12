//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests
{
    using Moq;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Documents;
    using Microsoft.Azure.Documents.Collections;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ThinClientTransportSerializerTests
    {
        private readonly Mock<ThinClientTransportSerializer.BufferProviderWrapper> mockBufferProviderWrapper;
        private readonly string testAccountName = "testAccount";
        private readonly Uri testUri = new Uri("http://localhost/dbs/db1/colls/coll1/docs/doc1");

        public ThinClientTransportSerializerTests()
        {
            this.mockBufferProviderWrapper = new Mock<ThinClientTransportSerializer.BufferProviderWrapper>();
        }

        [TestMethod]
        public async Task SerializeProxyRequestAsync_ShouldSerializeRequest()
        {
            // Arrange
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, this.testUri);
            requestMessage.Headers.Add(HandlerConstants.ProxyOperationType, "Read");
            requestMessage.Headers.Add(HandlerConstants.ProxyResourceType, "Document");
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[\"testPartitionKey\"]");

            // Act
            Stream result = await ThinClientTransportSerializer.SerializeProxyRequestAsync(
                this.mockBufferProviderWrapper.Object,
                this.testAccountName,
                requestMessage);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(Stream));
        }

        [TestMethod]
        public async Task SerializeProxyRequestAsync_ThrowsException_WhenPartitionKeyIsMissing()
        {
            // Arrange
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, this.testUri);
            requestMessage.Headers.Add(HandlerConstants.ProxyOperationType, "Read");
            requestMessage.Headers.Add(HandlerConstants.ProxyResourceType, "Document");
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InternalServerErrorException>(() =>
                ThinClientTransportSerializer.SerializeProxyRequestAsync(
                    this.mockBufferProviderWrapper.Object,
                    this.testAccountName,
                    requestMessage));
        }

        [TestMethod]
        public async Task SerializeProxyRequestAsync_InvalidOperationType_ThrowsException()
        {
            // Arrange
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, this.testUri);
            requestMessage.Headers.Add(HandlerConstants.ProxyOperationType, "InvalidOperation");
            requestMessage.Headers.Add(HandlerConstants.ProxyResourceType, "Document");
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[\"testPartitionKey\"]");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
                ThinClientTransportSerializer.SerializeProxyRequestAsync(
                    this.mockBufferProviderWrapper.Object,
                    this.testAccountName,
                    requestMessage));
        }

        [TestMethod]
        public async Task SerializeProxyRequestAsync_WithRequestBody_ShouldSerializeRequest()
        {
            // Arrange
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Post, this.testUri)
            {
                Content = new StringContent("{ \"key\": \"value\" }")
            };
            requestMessage.Headers.Add(HandlerConstants.ProxyOperationType, "Create");
            requestMessage.Headers.Add(HandlerConstants.ProxyResourceType, "Document");
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.ActivityId, Guid.NewGuid().ToString());
            requestMessage.Headers.Add(HttpConstants.HttpHeaders.PartitionKey, "[\"testPartitionKey\"]");

            // Act
            Stream result = await ThinClientTransportSerializer.SerializeProxyRequestAsync(
                this.mockBufferProviderWrapper.Object,
                this.testAccountName,
                requestMessage);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(Stream));
            Assert.IsTrue(result.Length > 0);
        }
    }
}