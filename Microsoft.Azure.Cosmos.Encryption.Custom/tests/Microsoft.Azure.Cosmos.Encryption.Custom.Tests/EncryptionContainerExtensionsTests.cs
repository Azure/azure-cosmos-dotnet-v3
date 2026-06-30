//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

#if NET8_0_OR_GREATER
namespace Microsoft.Azure.Cosmos.Encryption.Custom.Tests
{
    using System;
    using System.Linq;
    using Microsoft.Azure.Cosmos;
    using Microsoft.Azure.Cosmos.Encryption.Custom;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class EncryptionContainerExtensionsTests
    {
        [TestMethod]
        public void ToEncryptionStreamIterator_WithJsonProcessor_ThrowsForNonEncryptionContainer()
        {
            Container regularContainer = new Mock<Container>().Object;
            IQueryable<int> queryable = Enumerable.Empty<int>().AsQueryable();

            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => regularContainer.ToEncryptionStreamIterator(queryable, JsonProcessor.Stream));

            StringAssert.Contains(exception.Message, nameof(EncryptionContainer));
            StringAssert.Contains(exception.Message, nameof(EncryptionContainerExtensions.ToEncryptionStreamIterator));
        }

        [TestMethod]
        public void ToEncryptionFeedIterator_WithJsonProcessor_ThrowsForNonEncryptionContainer()
        {
            Container regularContainer = new Mock<Container>().Object;
            IQueryable<int> queryable = Enumerable.Empty<int>().AsQueryable();

            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => regularContainer.ToEncryptionFeedIterator(queryable, JsonProcessor.Stream));

            StringAssert.Contains(exception.Message, nameof(EncryptionContainer));
            StringAssert.Contains(exception.Message, nameof(EncryptionContainerExtensions.ToEncryptionFeedIterator));
        }
    }
}
#endif
