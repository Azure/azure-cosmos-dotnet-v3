//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.ChangeFeed.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Cosmos.ChangeFeed.LeaseManagement;
    using Microsoft.Azure.Cosmos.Tracing;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("ChangeFeed")]
    public class DocumentServiceLeaseStoreManagerBuilderTests
    {
        [TestMethod]
        public async Task InitializeAsync_WithContainerPartitionedById()
        {
            ContainerProperties containerProperties = new ContainerProperties
            {
                PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new System.Collections.ObjectModel.Collection<string>() { "/id" } }
            };


            Mock<ContainerInternal> leaseContainerMock = new Mock<ContainerInternal>();
            leaseContainerMock.Setup(c => c.GetCachedContainerPropertiesAsync(
                It.Is<bool>(b => b == false),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

            await DocumentServiceLeaseStoreManagerBuilder.InitializeAsync(
                Mock.Of<ContainerInternal>(),
                leaseContainerMock.Object,
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                ChangeFeedMode.LatestVersion);
        }

        [TestMethod]
        public async Task InitializeAsync_WithContainerPartitionedByPartitionKey()
        {
            ContainerProperties containerProperties = new ContainerProperties
            {
                PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new System.Collections.ObjectModel.Collection<string>() { "/partitionKey" } }
            };


            Mock<ContainerInternal> leaseContainerMock = new Mock<ContainerInternal>();
            leaseContainerMock.Setup(c => c.GetCachedContainerPropertiesAsync(
                It.Is<bool>(b => b == false),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

            await DocumentServiceLeaseStoreManagerBuilder.InitializeAsync(
                Mock.Of<ContainerInternal>(),
                leaseContainerMock.Object,
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                ChangeFeedMode.LatestVersion);
        }

        [TestMethod]
        public async Task InitializeAsync_WithContainerPartitionedBySystemPK()
        {
            ContainerProperties containerProperties = new ContainerProperties
            {
                PartitionKey = new Documents.PartitionKeyDefinition() { IsSystemKey = true }
            };


            Mock<ContainerInternal> leaseContainerMock = new Mock<ContainerInternal>();
            leaseContainerMock.Setup(c => c.GetCachedContainerPropertiesAsync(
                It.Is<bool>(b => b == false),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

            await DocumentServiceLeaseStoreManagerBuilder.InitializeAsync(
                Mock.Of<ContainerInternal>(),
                leaseContainerMock.Object,
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                ChangeFeedMode.LatestVersion);
        }

        [TestMethod]
        public async Task InitializeAsync_WithContainerPartitionedByRandom()
        {
            ContainerProperties containerProperties = new ContainerProperties
            {
                PartitionKey = new Documents.PartitionKeyDefinition() { Paths = new System.Collections.ObjectModel.Collection<string>() { "/random" } }
            };


            Mock<ContainerInternal> leaseContainerMock = new Mock<ContainerInternal>();
            leaseContainerMock.Setup(c => c.GetCachedContainerPropertiesAsync(
                It.Is<bool>(b => b == false),
                It.IsAny<ITrace>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(containerProperties);

            await Assert.ThrowsExceptionAsync<ArgumentException>(() => DocumentServiceLeaseStoreManagerBuilder.InitializeAsync(
                Mock.Of<ContainerInternal>(),
                leaseContainerMock.Object,
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                ChangeFeedMode.LatestVersion));
        }
    }
}