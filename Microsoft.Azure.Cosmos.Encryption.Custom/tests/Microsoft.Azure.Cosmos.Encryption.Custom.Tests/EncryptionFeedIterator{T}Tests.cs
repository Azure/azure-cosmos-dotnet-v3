namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EncryptionFeedIteratorTTests
    {
        [TestMethod]
        public void Ctor_Throws_OnNullParam()
        {
            EncryptionFeedIterator iterator = new (null, null, null);
            CosmosResponseFactory responseFactory = Mock.Of<CosmosResponseFactory>();

            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(null, responseFactory));
            Assert.ThrowsException<ArgumentNullException>(() => new EncryptionFeedIterator<Object>(iterator, null));
        }
    }
}
