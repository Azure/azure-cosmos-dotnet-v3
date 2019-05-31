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
    public class IncludedPathsFluentDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse()
        {
            Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> mockIndexingPolicyDefinition = new Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>();
            Action<IEnumerable<IncludedPath>> callback = (paths) =>
            {
                Assert.AreEqual("/path1", paths.First().Path);
                Assert.AreEqual("/path2", paths.Last().Path);
                Assert.AreEqual(2, paths.Count());
            };

            IncludedPathsFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> pathsFluentDefinitionCore = new IncludedPathsFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>(
                mockIndexingPolicyDefinition.Object,
                callback);

            pathsFluentDefinitionCore
                .Path("/path1")
                .Path("/path2")
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponseWithIndexes()
        {
            Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> mockIndexingPolicyDefinition = new Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>();
            Action<IEnumerable<IncludedPath>> callback = (paths) =>
            {
                IncludedPath first = paths.First();
                Assert.AreEqual("/path1", first.Path);
                Assert.AreEqual(3, first.Indexes.Count);
                Assert.AreEqual(IndexKind.Range, first.Indexes[0].Kind);
                Assert.AreEqual(DataType.Number, ((RangeIndex)first.Indexes[0]).DataType);
                Assert.AreEqual((short)20, ((RangeIndex)first.Indexes[0]).Precision);
                Assert.AreEqual(IndexKind.Hash, first.Indexes[1].Kind);
                Assert.AreEqual(DataType.Point, ((HashIndex)first.Indexes[1]).DataType);
                Assert.AreEqual((short)15, ((HashIndex)first.Indexes[1]).Precision);
                Assert.AreEqual(IndexKind.Spatial, first.Indexes[2].Kind);
                Assert.AreEqual(DataType.MultiPolygon, ((SpatialIndex)first.Indexes[2]).DataType);
                Assert.AreEqual(1, paths.Count());
            };

            IncludedPathsFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> pathsFluentDefinitionCore = new IncludedPathsFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>(
                mockIndexingPolicyDefinition.Object,
                callback);

            pathsFluentDefinitionCore
                .PathWithIndexes("/path1")
                    .RangeIndex(DataType.Number, 20)
                    .HashIndex(DataType.Point, 15)
                    .SpatialIndex(DataType.MultiPolygon)
                    .Attach()
                .Attach();
        }
    }
}
