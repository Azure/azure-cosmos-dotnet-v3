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
    public class SpatialIndexDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse_WithSpatialType()
        {
            Mock<IndexingPolicyDefinition<ContainerBuilder>> mockIndexingPolicyDefinition = new Mock<IndexingPolicyDefinition<ContainerBuilder>>();
            Action<SpatialPath> callback = (spatialspec) =>
            {
                Assert.AreEqual("/path", spatialspec.Path);
                Assert.AreEqual(2, spatialspec.SpatialTypes.Count);
                Assert.AreEqual(SpatialType.MultiPolygon, spatialspec.SpatialTypes[0]);
                Assert.AreEqual(SpatialType.Point, spatialspec.SpatialTypes[1]);
            };

            SpatialIndexDefinition<IndexingPolicyDefinition<ContainerBuilder>> spatialIndexFluentDefinitionCore = new SpatialIndexDefinition<IndexingPolicyDefinition<ContainerBuilder>>(
                mockIndexingPolicyDefinition.Object,
                callback);

            spatialIndexFluentDefinitionCore
                .Path("/path", SpatialType.MultiPolygon, SpatialType.Point)
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithNoSpatialType()
        {
            Mock<IndexingPolicyDefinition<ContainerBuilder>> mockIndexingPolicyDefinition = new Mock<IndexingPolicyDefinition<ContainerBuilder>>();
            Action<SpatialPath> callback = (spatialspec) =>
            {
                Assert.AreEqual("/path", spatialspec.Path);
                Assert.AreEqual(0, spatialspec.SpatialTypes.Count);
            };

            SpatialIndexDefinition<IndexingPolicyDefinition<ContainerBuilder>> spatialIndexFluentDefinitionCore = new SpatialIndexDefinition<IndexingPolicyDefinition<ContainerBuilder>>(
                mockIndexingPolicyDefinition.Object,
                callback);

            spatialIndexFluentDefinitionCore
                .Path("/path")
                .Attach();
        }
    }
}