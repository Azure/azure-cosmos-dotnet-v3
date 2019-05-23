//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Fluent
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class PathsFluentDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse()
        {
            Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> mockIndexingPolicyDefinition = new Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>();
            Action<IEnumerable<string>> callback = (paths) =>
            {
                Assert.AreEqual("/path1", paths.First());
                Assert.AreEqual("/path2", paths.Last());
                Assert.AreEqual(2, paths.Count());
            };

            PathsFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> pathsFluentDefinitionCore = new PathsFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>(
                mockIndexingPolicyDefinition.Object,
                callback);

            pathsFluentDefinitionCore
                .Path("/path1")
                .Path("/path2")
                .Attach();
        }
    }
}
