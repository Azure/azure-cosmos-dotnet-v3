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
            Mock<CosmosContainerFluentDefinition> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinition>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.IsFalse(policy.Automatic);
                Assert.AreEqual(IndexingMode.None, policy.IndexingMode);
            };

            IndexingPolicyFluentDefinitionCore indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinitionCore(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .WithIndexingMode(IndexingMode.None)
                .WithoutAutomaticIndexing()
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithSpatialIndexes()
        {
            Mock<CosmosContainerFluentDefinition> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinition>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.SpatialIndexes.Count);
                Assert.AreEqual("/path", policy.SpatialIndexes[0].Path);
            };

            IndexingPolicyFluentDefinitionCore indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinitionCore(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .WithSpatialIndex()
                    .WithPath("/path")
                    .Attach()
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithExcludedPaths()
        {
            Mock<CosmosContainerFluentDefinition> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinition>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.ExcludedPaths.Count);
                Assert.AreEqual("/path", policy.ExcludedPaths[0].Path);
            };

            IndexingPolicyFluentDefinitionCore indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinitionCore(
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
            Mock<CosmosContainerFluentDefinition> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinition>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.IncludedPaths.Count);
                Assert.AreEqual("/path", policy.IncludedPaths[0].Path);
            };

            IndexingPolicyFluentDefinitionCore indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinitionCore(
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
            Mock<CosmosContainerFluentDefinition> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinition>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.CompositeIndexes.Count);
                Assert.AreEqual("/path", policy.CompositeIndexes[0][0].Path);
            };

            IndexingPolicyFluentDefinitionCore indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinitionCore(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .WithCompositeIndex()
                    .Path("/path")
                    .Attach()
                .Attach();
        }
    }
}
