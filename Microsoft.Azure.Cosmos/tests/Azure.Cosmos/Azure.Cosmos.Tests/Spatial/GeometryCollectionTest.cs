//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

namespace Azure.Cosmos.Test.Spatial
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Azure.Cosmos.Spatial;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Tests <see cref="GeometryCollection"/> class and serialization.
    /// </summary>
    [TestClass]
    public class GeometryCollectionTest : CommonSerializationTest
    {
        /// <summary>
        /// Tests serialization/deserialization.
        /// </summary>
        [TestMethod]
        public void TestGeometryCollectionSerialization()
        {
            string json =
                @"{
                   ""type"":""GeometryCollection"",
                   ""geometries"":[{""type"":""Point"", ""coordinates"":[20, 20]}],
                   ""bbox"":[20, 20, 30, 30]
                  }";

            GeometryCollection geometryCollection = JsonSerializer.Deserialize<GeometryCollection>(json, this.restContractOptions);

            Assert.AreEqual(1, geometryCollection.Geometries.Count);
            Assert.IsInstanceOfType(geometryCollection.Geometries[0], typeof(Point));
            Assert.AreEqual(new Position(20, 20), (geometryCollection.Geometries[0] as Point).Position);

            GeoJson geom = JsonSerializer.Deserialize<GeoJson>(json, this.restContractOptions);
            Assert.AreEqual(GeoJsonType.GeometryCollection, geom.Type);

            Assert.AreEqual(geom, geometryCollection);

            string json1 = JsonSerializer.Serialize(geometryCollection, this.restContractOptions);
            GeoJson geom1 = JsonSerializer.Deserialize<GeoJson>(json1, this.restContractOptions);
            Assert.AreEqual(geom1, geom);
        }

        /// <summary>
        /// Tests equality/hash code.
        /// </summary>
        [TestMethod]
        public void TestGeometryCollectionEqualsHashCode()
        {
            GeometryCollection geometryCollection1 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            GeometryCollection geometryCollection2 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            GeometryCollection geometryCollection3 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 41) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            GeometryCollection geometryCollection4 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            GeometryCollection geometryCollection5 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 41)));

            GeometryCollection geometryCollection6 = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            Assert.AreEqual(geometryCollection1, geometryCollection2);
            Assert.AreEqual(geometryCollection1.GetHashCode(), geometryCollection2.GetHashCode());

            Assert.AreNotEqual(geometryCollection1, geometryCollection3);
            Assert.AreNotEqual(geometryCollection1.GetHashCode(), geometryCollection3.GetHashCode());

            Assert.AreNotEqual(geometryCollection1, geometryCollection4);
            Assert.AreNotEqual(geometryCollection1.GetHashCode(), geometryCollection4.GetHashCode());

            Assert.AreNotEqual(geometryCollection1, geometryCollection5);
            Assert.AreNotEqual(geometryCollection1.GetHashCode(), geometryCollection5.GetHashCode());

            Assert.AreNotEqual(geometryCollection1, geometryCollection6);
            Assert.AreNotEqual(geometryCollection1.GetHashCode(), geometryCollection6.GetHashCode());
        }

        /// <summary>
        /// Tests constructor arguments.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void TestGeometryCollectionConstructorNullException()
        {
            new GeometryCollection(null);
        }

        /// <summary>
        /// Tests construction.
        /// </summary>
        [TestMethod]
        public void TestGeometryCollectionConstructors()
        {
            GeometryCollection geometryCollection = new GeometryCollection(
                new[] { new Point(20, 30), new Point(30, 40) },
                new BoundingBox(new Position(0, 0), new Position(40, 40)));

            Assert.AreEqual(new Point(20, 30), geometryCollection.Geometries[0]);
            Assert.AreEqual(new Point(30, 40), geometryCollection.Geometries[1]);

            Assert.AreEqual(new Position(0, 0), geometryCollection.BoundingBox.Min);
            Assert.AreEqual(new Position(40, 40), geometryCollection.BoundingBox.Max);
        }
    }
}
