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
    public class IndexingPolicyFluentDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse_WithIndexingMode()
        {
            Mock<CosmosContainerFluentDefinitionForCreate> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinitionForCreate>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.IsFalse(policy.Automatic);
                Assert.AreEqual(IndexingMode.None, policy.IndexingMode);
            };

            IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate> indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .IndexingMode(IndexingMode.None)
                .AutomaticIndexing(false)
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithSpatialIndexes()
        {
            Mock<CosmosContainerFluentDefinitionForCreate> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinitionForCreate>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.SpatialIndexes.Count);
                Assert.AreEqual("/path", policy.SpatialIndexes[0].Path);
            };

            IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate> indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .SpatialIndex()
                    .Path("/path")
                    .Attach()
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithExcludedPaths()
        {
            Mock<CosmosContainerFluentDefinitionForCreate> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinitionForCreate>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.ExcludedPaths.Count);
                Assert.AreEqual("/path", policy.ExcludedPaths[0].Path);
            };

            IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate> indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .ExcludedPaths()
                    .Path("/path")
                    .Attach()
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithIncludedPaths()
        {
            Mock<CosmosContainerFluentDefinitionForCreate> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinitionForCreate>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.IncludedPaths.Count);
                Assert.AreEqual("/path", policy.IncludedPaths[0].Path);
            };

            IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate> indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .IncludedPaths()
                    .Path("/path")
                    .Attach()
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithCompositeIndex()
        {
            Mock<CosmosContainerFluentDefinitionForCreate> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinitionForCreate>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.CompositeIndexes.Count);
                Assert.AreEqual("/path", policy.CompositeIndexes[0][0].Path);
            };

            IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate> indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .CompositeIndex()
                    .Path("/path")
                    .Attach()
                .Attach();
        }
    }
}
