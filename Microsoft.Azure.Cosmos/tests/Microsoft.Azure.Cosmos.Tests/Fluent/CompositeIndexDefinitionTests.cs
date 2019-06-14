//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Microsoft.Azure.Cosmos.Tests.Fluent
{
    using System;
    using System.Collections.ObjectModel;
    using Microsoft.Azure.Cosmos.Fluent;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    public class CompositeIndexDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse()
        {
            Mock<IndexingPolicyDefinition<CreateContainerDefinition>> mockIndexingPolicyDefinition = new Mock<IndexingPolicyDefinition<CreateContainerDefinition>>();
            Action<Collection<CompositePath>> callback = (paths) =>
            {
                Assert.AreEqual(2, paths.Count);
                Assert.AreEqual("/path1", paths[0].Path);
                Assert.AreEqual("/path2", paths[1].Path);
                Assert.AreEqual(CompositePathSortOrder.Descending, paths[1].Order);                
            };

            CompositeIndexDefinition<IndexingPolicyDefinition<CreateContainerDefinition>> compositeIndexFluentDefinitionCore = new CompositeIndexDefinition<IndexingPolicyDefinition<CreateContainerDefinition>>(
                mockIndexingPolicyDefinition.Object,
                callback);

            compositeIndexFluentDefinitionCore
                .Path("/path1")
                .Path("/path2", CompositePathSortOrder.Descending)
                .Attach();
        }
    }
}
