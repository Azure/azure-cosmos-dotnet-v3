//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Tests.Spatial
{
    using System;
    using System.Text.Json;
    using Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests <see cref="Polygon"/> class and serialization.
    /// </summary>
    [TestClass]
    public class PolygonTest : CommonSerializationTest
    {
        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestPolygonSerialization()
        {
            string json = @"{
                    ""type"":""Polygon"",
                    ""coordinates"":[[[20,30], [30,30], [30,20], [20,20], [20, 30]], [[25,28], [25,25], [25, 28], [28,28], [25, 28]]],
                    ""bbox"":[20, 20, 30, 30]
            }";

            Polygon polygon = JsonSerializer.Deserialize<Polygon>(json, this.restContractOptions);

            Assert.AreEqual(2, polygon.Coordinates.Count);
            Assert.AreEqual(5, polygon.Coordinates[0].Count);
            Assert.AreEqual(new Position(20, 30), polygon.Coordinates[0][0]);
            Assert.AreEqual(new Position(30, 20), polygon.Coordinates[0][2]);

            Assert.AreEqual((20, 20), polygon.BoundingBox.SouthwesterlyPoint);
            Assert.AreEqual((30, 30), polygon.BoundingBox.NortheasertlyPoint);

            GeoJson geom = JsonSerializer.Deserialize<GeoJson>(json, this.restContractOptions);
            Assert.AreEqual(GeoJsonType.Polygon, geom.Type);

            Assert.AreEqual(geom, polygon);

            string json1 = JsonSerializer.Serialize(polygon, this.restContractOptions);
            GeoJson geom1 = JsonSerializer.Deserialize<GeoJson>(json1, this.restContractOptions);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestPolygonEqualsHashCode()
        {
            Polygon polygon1 =
                new Polygon(
                    new LinearRing(
                        new[]
                        {
                            new Position(20, 20),
                            new Position(20, 21),
                            new Position(21, 21),
                            new Position(21, 20),
                            new Position(20, 20)
                        }),
                    new BoundingBox((0, 0), (40, 40)));

            Polygon polygon2 = new Polygon(
                new LinearRing(
                    new[]
                    {
                        new Position(20, 20),
                        new Position(20, 21),
                        new Position(21, 21),
                        new Position(21, 20),
                        new Position(20, 20)
                    }),
                new BoundingBox((0, 0), (40, 40)));

            Polygon polygon3 = new Polygon(
                new LinearRing(
                    new[]
                    {
                        new Position(20, 20),
                        new Position(20, 22),
                        new Position(21, 21),
                        new Position(21, 20),
                        new Position(20, 20)
                    }),
                new BoundingBox((0, 0), (40, 40)));

            Polygon polygon5 = new Polygon(
                new LinearRing(
                    new[]
                    {
                        new Position(20, 20),
                        new Position(20, 21),
                        new Position(21, 21),
                        new Position(21, 20),
                        new Position(20, 20)
                    }),
                new BoundingBox((0, 0), (40, 41)));

            Assert.AreEqual(polygon1, polygon2);
            Assert.AreEqual(polygon1.GetHashCode(), polygon2.GetHashCode());

            Assert.AreNotEqual(polygon1, polygon3);
            Assert.AreNotEqual(polygon1.GetHashCode(), polygon3.GetHashCode());

            Assert.AreNotEqual(polygon1, polygon5);
            Assert.AreNotEqual(polygon1.GetHashCode(), polygon5.GetHashCode());
        }

        /// <summary>
        /// Tests constructor exceptions.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestPolygonConstructorNullException()
        {
            new Polygon(exteriorRing: null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestPolygonConstructors()
        {
            Polygon polygon = new Polygon(
                new LinearRing(
                    new[]
                    {
                        new Position(20, 20),
                        new Position(20, 21),
                        new Position(21, 21),
                        new Position(21, 20),
                        new Position(20, 20)
                    }),
                new BoundingBox((0, 0), (40, 40)));

            Assert.AreEqual(new Position(20, 20), polygon.Coordinates[0][0]);

            Assert.AreEqual((0, 0), polygon.BoundingBox.SouthwesterlyPoint);
            Assert.AreEqual((40, 40), polygon.BoundingBox.NortheasertlyPoint);
        }
    }
}
