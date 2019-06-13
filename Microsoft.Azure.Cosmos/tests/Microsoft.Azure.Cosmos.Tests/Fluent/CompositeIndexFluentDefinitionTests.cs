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
    public class CompositeIndexFluentDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse()
        {
            Mock<IndexingPolicyFluentDefinition<ContainerFluentDefinitionForCreate>> mockIndexingPolicyDefinition = new Mock<IndexingPolicyFluentDefinition<ContainerFluentDefinitionForCreate>>();
            Action<Collection<CompositePath>> callback = (paths) =>
            {
                Assert.AreEqual(2, paths.Count);
                Assert.AreEqual("/path1", paths[0].Path);
                Assert.AreEqual("/path2", paths[1].Path);
                Assert.AreEqual(CompositePathSortOrder.Descending, paths[1].Order);                
            };

            CompositeIndexFluentDefinition<IndexingPolicyFluentDefinition<ContainerFluentDefinitionForCreate>> compositeIndexFluentDefinitionCore = new CompositeIndexFluentDefinition<IndexingPolicyFluentDefinition<ContainerFluentDefinitionForCreate>>(
                mockIndexingPolicyDefinition.Object,
                callback);

            compositeIndexFluentDefinitionCore
                .Path("/path1")
                .Path("/path2", CompositePathSortOrder.Descending)
                .Attach();
        }
    }
}
