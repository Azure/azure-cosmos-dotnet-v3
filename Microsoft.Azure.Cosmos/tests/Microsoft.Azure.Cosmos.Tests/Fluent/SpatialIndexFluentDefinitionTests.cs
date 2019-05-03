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
    public class SpatialIndexFluentDefinitionTests
    {
        [TestMethod]
        public void AttachReturnsCorrectResponse_WithSpatialType()
        {
            Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> mockIndexingPolicyDefinition = new Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>();
            Action<SpatialSpec> callback = (spatialspec) =>
            {
                Assert.AreEqual("/path", spatialspec.Path);
                Assert.AreEqual(2, spatialspec.SpatialTypes.Count);
                Assert.AreEqual(SpatialType.MultiPolygon, spatialspec.SpatialTypes[0]);
                Assert.AreEqual(SpatialType.Point, spatialspec.SpatialTypes[1]);
            };

            SpatialIndexFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> spatialIndexFluentDefinitionCore = new SpatialIndexFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>(
                mockIndexingPolicyDefinition.Object,
                callback);

            spatialIndexFluentDefinitionCore
                .Path("/path", SpatialType.MultiPolygon, SpatialType.Point)
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithNoSpatialType()
        {
            Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> mockIndexingPolicyDefinition = new Mock<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>();
            Action<SpatialSpec> callback = (spatialspec) =>
            {
                Assert.AreEqual("/path", spatialspec.Path);
                Assert.AreEqual(0, spatialspec.SpatialTypes.Count);
            };

            SpatialIndexFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>> spatialIndexFluentDefinitionCore = new SpatialIndexFluentDefinition<IndexingPolicyFluentDefinition<CosmosContainerFluentDefinitionForCreate>>(
                mockIndexingPolicyDefinition.Object,
                callback);

            spatialIndexFluentDefinitionCore
                .Path("/path")
                .Attach();
        }
    }
}
