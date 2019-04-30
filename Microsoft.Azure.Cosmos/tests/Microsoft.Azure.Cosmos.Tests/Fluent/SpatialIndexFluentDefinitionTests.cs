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
            Mock<IndexingPolicyFluentDefinition> mockIndexingPolicyDefinition = new Mock<IndexingPolicyFluentDefinition>();
            Action<SpatialSpec> callback = (spatialspec) =>
            {
                Assert.AreEqual("/path", spatialspec.Path);
                Assert.AreEqual(2, spatialspec.SpatialTypes.Count);
                Assert.AreEqual(SpatialType.MultiPolygon, spatialspec.SpatialTypes[0]);
                Assert.AreEqual(SpatialType.Point, spatialspec.SpatialTypes[1]);
            };

            SpatialIndexFluentDefinitionCore spatialIndexFluentDefinitionCore = new SpatialIndexFluentDefinitionCore(
                mockIndexingPolicyDefinition.Object,
                callback);

            spatialIndexFluentDefinitionCore
                .WithPath("/path", SpatialType.MultiPolygon, SpatialType.Point)
                .Attach();
        }

        [TestMethod]
        public void AttachReturnsCorrectResponse_WithNoSpatialType()
        {
            Mock<IndexingPolicyFluentDefinition> mockIndexingPolicyDefinition = new Mock<IndexingPolicyFluentDefinition>();
            Action<SpatialSpec> callback = (spatialspec) =>
            {
                Assert.AreEqual("/path", spatialspec.Path);
                Assert.AreEqual(0, spatialspec.SpatialTypes.Count);
            };

            SpatialIndexFluentDefinitionCore spatialIndexFluentDefinitionCore = new SpatialIndexFluentDefinitionCore(
                mockIndexingPolicyDefinition.Object,
                callback);

            spatialIndexFluentDefinitionCore
                .WithPath("/path")
                .Attach();
        }
    }
}
