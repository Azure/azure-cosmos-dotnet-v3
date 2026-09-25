//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Azure.Core.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EncryptionContainerTests
    {
        [TestMethod]
        public void EncryptionContainer_CanBeCreated()
        {
            Container container = CreateEncryptionContainer(new Mock<Container>());

            Assert.IsInstanceOfType(container, typeof(EncryptionContainer));
        }

#if PREVIEW || SDKPROJECTREF
        [TestMethod]
        public async Task GetPartitionKeyRangesAsync_ForwardsFeedRangeAndCancellationToken()
        {
            Mock<Container> innerContainer = new Mock<Container>();
            Container container = CreateEncryptionContainer(innerContainer);
            FeedRange feedRange = FeedRange.FromPartitionKey(new PartitionKey("test"));
            using CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            IEnumerable<string> expectedRangeIds = new[] { "0", "1" };
            innerContainer
                .Setup(c => c.GetPartitionKeyRangesAsync(feedRange, cancellationTokenSource.Token))
                .ReturnsAsync(expectedRangeIds);

            IEnumerable<string> rangeIds = await container.GetPartitionKeyRangesAsync(
                feedRange,
                cancellationTokenSource.Token);

            Assert.AreSame(expectedRangeIds, rangeIds);
            innerContainer.Verify(
                c => c.GetPartitionKeyRangesAsync(feedRange, cancellationTokenSource.Token),
                Times.Once);
        }
#endif

        private static Container CreateEncryptionContainer(Mock<Container> innerContainer)
        {
            Mock<CosmosClient> client = new Mock<CosmosClient>();
            client.SetupGet(c => c.ClientOptions).Returns(new CosmosClientOptions());
            Mock<Database> database = new Mock<Database>();
            database.SetupGet(d => d.Client).Returns(client.Object);
            innerContainer.SetupGet(c => c.Database).Returns(database.Object);
            EncryptionCosmosClient encryptionClient = new EncryptionCosmosClient(
                client.Object,
                Mock.Of<IKeyEncryptionKeyResolver>(),
                "test",
                keyCacheTimeToLive: null);

            return new EncryptionContainer(innerContainer.Object, encryptionClient);
        }
    }
}
