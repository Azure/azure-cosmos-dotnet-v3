//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Test.Spatial
{
    using System;
    using System.Text.Json;
    using Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests <see cref="Point"/> class and serialization.
    /// </summary>
    [TestClass]
    public class PointTest : CommonSerializationTest
    {
        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestPointSerialization()
        {
            string json = @"{
                    ""type"":""Point"",
                    ""coordinates"":[20.232323232323232,30.3],
                    ""bbox"":[20, 20, 30, 30]
            }";
            Point point = JsonSerializer.Deserialize<Point>(json, this.restContractOptions);

            Assert.AreEqual(2, point.Coordinates.Count);
            Assert.AreEqual(20.232323232323232, point.Coordinates.Easting);
            Assert.AreEqual(30.3, point.Coordinates.Northing);
            Assert.AreEqual((20, 20), point.BoundingBox.SouthwesterlyPoint);
            Assert.AreEqual((30, 30), point.BoundingBox.NortheasertlyPoint);

            GeoJson geom = JsonSerializer.Deserialize<GeoJson>(json, this.restContractOptions);
            Assert.AreEqual(GeoJsonType.Point, geom.Type);

            Assert.AreEqual(geom, point);

            string json1 = JsonSerializer.Serialize(point, this.restContractOptions);
            GeoJson geom1 = JsonSerializer.Deserialize<GeoJson>(json1, this.restContractOptions);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestPointEqualsHashCode()
        {
            Point point1 = new Point(
                new Position(20, 30),
                new BoundingBox((0, 0), (40, 40)));

            Point point2 = new Point(
                new Position(20, 30),
                new BoundingBox((0, 0), (40, 40)));

            Point point3 = new Point(
                new Position(20, 31),
                new BoundingBox((0, 0), (40, 40)));

            Point point4 = new Point(
                new Position(20, 31),
                new BoundingBox((0, 0), (40, 40)));

            Point point5 = new Point(
                new Position(20, 30),
                new BoundingBox((0, 0), (40, 41)));

            Point point6 = new Point(
                new Position(20, 30),
                new BoundingBox((0, 0), (40, 40)));

            Assert.AreEqual(point1, point2);
            Assert.AreEqual(point1.GetHashCode(), point2.GetHashCode());

            Assert.AreNotEqual(point1, point3);
            Assert.AreNotEqual(point1.GetHashCode(), point3.GetHashCode());

            Assert.AreNotEqual(point1, point4);
            Assert.AreNotEqual(point1.GetHashCode(), point4.GetHashCode());

            Assert.AreNotEqual(point1, point5);
            Assert.AreNotEqual(point1.GetHashCode(), point5.GetHashCode());

            Assert.AreNotEqual(point1, point6);
            Assert.AreNotEqual(point1.GetHashCode(), point6.GetHashCode());
        }

        /// <summary>
        /// Tests constructor exceptions.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestPointConstructorNullException()
        {
            new Point(null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestPointConstructors()
        {
            Point point = new Point(
                new Position(20, 30),
                new BoundingBox((0, 0), (40, 40)));

            Assert.AreEqual(20, point.Coordinates.Easting);
            Assert.AreEqual(30, point.Coordinates.Northing);
            Assert.AreEqual((0, 0), point.BoundingBox.SouthwesterlyPoint);
            Assert.AreEqual((40, 40), point.BoundingBox.NortheasertlyPoint);
        }
    }
}
