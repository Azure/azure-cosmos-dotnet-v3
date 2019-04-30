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
            Mock<CosmosContainerFluentDefinition> mockContainerDefinition = new Mock<CosmosContainerFluentDefinition>();
            Action<UniqueKey> callback = (uniqueKey) =>
            {
                Assert.AreEqual(2, uniqueKey.Paths.Count);
                Assert.AreEqual("/property1", uniqueKey.Paths[0]);
                Assert.AreEqual("/property2", uniqueKey.Paths[1]);
            };

            UniqueKeyFluentDefinitionCore uniqueKeyFluentDefinitionCore = new UniqueKeyFluentDefinitionCore(
                mockContainerDefinition.Object,
                callback);

            uniqueKeyFluentDefinitionCore
                .Path("/property1")
                .Path("/property2")
                .Attach();
        }
    }
}
