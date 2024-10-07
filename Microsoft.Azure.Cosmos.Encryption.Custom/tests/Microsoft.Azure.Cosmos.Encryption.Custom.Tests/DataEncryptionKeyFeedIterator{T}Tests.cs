namespace Microsoft.Azure.Cosmos.Encryption.Tests
{
    using System;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class DataEncryptionKeyFeedIteratorTTests
    {
        [TestMethod]
        public void Ctor_Throws_OnNullParam()
        {
            DataEncryptionKeyFeedIterator iterator = new (null);
            CosmosResponseFactory responseFactory = Mock.Of<CosmosResponseFactory>();

            Assert.ThrowsException<ArgumentNullException>(() => new DataEncryptionKeyFeedIterator<Object>(null, responseFactory));
            Assert.ThrowsException<ArgumentNullException>(() => new DataEncryptionKeyFeedIterator<Object>(iterator, null));
        }
    }
}
