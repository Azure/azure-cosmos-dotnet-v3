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
    public class IndexingPolicyDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse_WithIndexingMode()
        {
            Mock<ContainerBuilder> mockContainerPolicyDefinition = new Mock<ContainerBuilder>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.IsFalse(policy.Automatic);
                Assert.AreEqual(IndexingMode.None, policy.IndexingMode);
            };

            IndexingPolicyDefinition<ContainerBuilder> indexingPolicyFluentDefinitionCore = new IndexingPolicyDefinition<ContainerBuilder>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .WithIndexingMode(IndexingMode.None)
                .WithAutomaticIndexing(false)
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithSpatialIndexes()
        {
            Mock<ContainerBuilder> mockContainerPolicyDefinition = new Mock<ContainerBuilder>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.SpatialIndexes.Count);
                Assert.AreEqual("/path", policy.SpatialIndexes[0].Path);
            };

            IndexingPolicyDefinition<ContainerBuilder> indexingPolicyFluentDefinitionCore = new IndexingPolicyDefinition<ContainerBuilder>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .WithSpatialIndex()
                    .Path("/path")
                    .Attach()
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithExcludedPaths()
        {
            Mock<ContainerBuilder> mockContainerPolicyDefinition = new Mock<ContainerBuilder>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.ExcludedPaths.Count);
                Assert.AreEqual("/path", policy.ExcludedPaths[0].Path);
            };

            IndexingPolicyDefinition<ContainerBuilder> indexingPolicyFluentDefinitionCore = new IndexingPolicyDefinition<ContainerBuilder>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .WithExcludedPaths()
                    .Path("/path")
                    .Attach()
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithIncludedPaths()
        {
            Mock<ContainerBuilder> mockContainerPolicyDefinition = new Mock<ContainerBuilder>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.IncludedPaths.Count);
                Assert.AreEqual("/path", policy.IncludedPaths[0].Path);
            };

            IndexingPolicyDefinition<ContainerBuilder> indexingPolicyFluentDefinitionCore = new IndexingPolicyDefinition<ContainerBuilder>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .WithIncludedPaths()
                    .Path("/path")
                    .Attach()
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithCompositeIndex()
        {
            Mock<ContainerBuilder> mockContainerPolicyDefinition = new Mock<ContainerBuilder>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.CompositeIndexes.Count);
                Assert.AreEqual("/path", policy.CompositeIndexes[0][0].Path);
            };

            IndexingPolicyDefinition<ContainerBuilder> indexingPolicyFluentDefinitionCore = new IndexingPolicyDefinition<ContainerBuilder>(
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