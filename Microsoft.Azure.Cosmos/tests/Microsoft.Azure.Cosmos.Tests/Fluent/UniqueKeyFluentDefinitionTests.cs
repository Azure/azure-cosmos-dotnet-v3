//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Fluent
{
    using System;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class UniqueKeyFluentDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse()
        {
            Mock<CosmosContainerFluentDefinitionForCreate> mockContainerDefinition = new Mock<CosmosContainerFluentDefinitionForCreate>();
            Action<UniqueKey> callback = (uniqueKey) =>
            {
                Assert.AreEqual(2, uniqueKey.Paths.Count);
                Assert.AreEqual("/property1", uniqueKey.Paths[0]);
                Assert.AreEqual("/property2", uniqueKey.Paths[1]);
            };

            UniqueKeyFluentDefinition uniqueKeyFluentDefinitionCore = new UniqueKeyFluentDefinition(
                mockContainerDefinition.Object,
                callback);

            uniqueKeyFluentDefinitionCore
                .Path("/property1")
                .Path("/property2")
                .Attach();
        }
    }
}
