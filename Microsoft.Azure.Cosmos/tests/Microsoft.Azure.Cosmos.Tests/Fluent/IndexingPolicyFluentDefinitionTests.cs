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
                .WithIndexingMode(IndexingMode.None)
                .WithAutomaticIndexing(false)
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
                .WithSpatialIndex()
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
                .WithExcludedPaths()
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
                .WithIncludedPaths()
                    .Path("/path")
                    .Attach()
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithIncludedPathsWithIndexes()
        {
            Mock<CosmosContainerFluentDefinitionForCreate> mockContainerPolicyDefinition = new Mock<CosmosContainerFluentDefinitionForCreate>();
            Action<IndexingPolicy> callback = (policy) =>
            {
                Assert.AreEqual(1, policy.IncludedPaths.Count);
                Assert.AreEqual("/path1", policy.IncludedPaths[0].Path);
                Assert.AreEqual(3, policy.IncludedPaths[0].Indexes.Count);
                Assert.AreEqual(IndexKind.Range, policy.IncludedPaths[0].Indexes[0].Kind);
                Assert.AreEqual(DataType.Number, ((RangeIndex)policy.IncludedPaths[0].Indexes[0]).DataType);
                Assert.AreEqual((short)20, ((RangeIndex)policy.IncludedPaths[0].Indexes[0]).Precision);
                Assert.AreEqual(IndexKind.Hash, policy.IncludedPaths[0].Indexes[1].Kind);
                Assert.AreEqual(DataType.Point, ((HashIndex)policy.IncludedPaths[0].Indexes[1]).DataType);
                Assert.AreEqual(IndexKind.Spatial, policy.IncludedPaths[0].Indexes[2].Kind);
                Assert.AreEqual(DataType.MultiPolygon, ((SpatialIndex)policy.IncludedPaths[0].Indexes[2]).DataType);
            };

            IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate> indexingPolicyFluentDefinitionCore = new IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>(
                mockContainerPolicyDefinition.Object,
                callback);

            indexingPolicyFluentDefinitionCore
                .WithIncludedPaths()
                    .PathWithIndexes("/path1")
                        .RangeIndex(DataType.Number, 20)
                        .HashIndex(DataType.Point)
                        .SpatialIndex(DataType.MultiPolygon)
                        .Attach()
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
                .WithCompositeIndex()
                    .Path("/path")
                    .Attach()
                .Attach();
        }
    }
}
